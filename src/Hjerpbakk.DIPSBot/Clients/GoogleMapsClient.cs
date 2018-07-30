﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Hjerpbakk.DIPSBot.Configuration;
using Hjerpbakk.DIPSBot.Extensions;
using Hjerpbakk.DIPSBot.Model.BikeShare;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace Hjerpbakk.DIPSBot.Clients {
    public class GoogleMapsClient {
        const int MaxResultSize = 3;

        readonly string baseDistanceQueryString;
        readonly string baseRouteQueryString;
        readonly string baseImageUrl;

        readonly HttpClient httpClient;
        readonly IMemoryCache memoryCache;

        public GoogleMapsClient(IGoogleMapsConfiguration googleMapsConfiguration, HttpClient httpClient, IMemoryCache memoryCache) {
            this.httpClient = httpClient;

            var region = string.IsNullOrEmpty(googleMapsConfiguration.MapsRegion) ? string.Empty : "&region=" + googleMapsConfiguration.MapsRegion;
            baseDistanceQueryString = "https://maps.googleapis.com/maps/api/distancematrix/json?origins={0}&destinations={1}" + region + "&mode=walking&units=metric&key=" + googleMapsConfiguration.GoogleMapsApiKey;
            baseRouteQueryString = "https://maps.googleapis.com/maps/api/directions/json?origin={0}&destination={1}" + region + "&mode=walking&units=metric&key=" + googleMapsConfiguration.GoogleMapsApiKey;
            baseImageUrl = "https://maps.googleapis.com/maps/api/staticmap?size=600x600&scale=2&maptype=roadmap" + region + "&{0}&{1}&path=weight:5%7Ccolor:blue%7Cenc:{2}&key=" + googleMapsConfiguration.GoogleMapsApiKey;
            this.memoryCache = memoryCache;
        }

        public async Task<BikeShareStationWithWalkingDuration[]> FindBikeSharingStationsNearestToAddress(string fromAddress, AllStationsInArea allStationsInArea) {
            if (string.IsNullOrEmpty(fromAddress)) {
                throw new ArgumentNullException(nameof(fromAddress));
            }

            var encodedAddress = HttpUtility.UrlEncode(fromAddress);
            var routeDistances = await memoryCache.GetOrSet(encodedAddress, FindRoutesToAllStations);

            var sortedStations = SortStationsByDistanceFromUser();

            var resultLength = sortedStations.Length < MaxResultSize ? sortedStations.Length : MaxResultSize;
            var nearestStations = new BikeShareStationWithWalkingDuration[resultLength];
            for (int i = 0; i < nearestStations.Length; i++) {
                nearestStations[i] = new BikeShareStationWithWalkingDuration(
                    allStationsInArea.BikeShareStations[sortedStations[i].index],
                    sortedStations[i].duration);
            }

            return nearestStations;

            async Task<Element[]> FindRoutesToAllStations() {
                var queryString = string.Format(baseDistanceQueryString, encodedAddress, allStationsInArea.PipedCoordinatesToAllStations);
                var response = await httpClient.GetStringAsync(queryString);
                var routeDistance = JsonConvert.DeserializeObject<RouteDistance>(response);

                if (routeDistance.Rows.Length == 0) {
                    throw new InvalidOperationException($"Could not find any routes from {fromAddress} to any bike sharing stations.");
                }

                return routeDistance.Rows[0].Elements;
            }

            (long duration, int index)[] SortStationsByDistanceFromUser() {
                var reachableStations = new List<(long duration, int index)>();
                for (int i = 0; i < routeDistances.Length; i++) {
                    var element = routeDistances[i];
                    if (element.Status != "OK") {
                        continue;
                    }

                    reachableStations.Add((element.Duration.Value, i));
                }

                return reachableStations.OrderBy(s => s.duration).ToArray(); ;
            }
        }

        public async Task<string> CreateImageWithDirections(string from, LabelledBikeShareStation[] nearestBikeStations) {
            if (string.IsNullOrEmpty(from)) {
                throw new ArgumentNullException(nameof(from));
            }

            if (nearestBikeStations == null || nearestBikeStations.Length == 0 || nearestBikeStations.Length > MaxResultSize) {
                throw new ArgumentException($"Number of bike sharing stations must be between 1 and {MaxResultSize}", nameof(nearestBikeStations));
            }

            var routePolyline = await memoryCache.GetOrSet(from + "directions", FindDetailedRouteToStation);
            var userMarker = $"markers=color:green%7Clabel:U%7C{from}";
            var stationMarkers = string.Join("&", nearestBikeStations.Select(bikeShareStation => $"markers=color:red%7Clabel:{bikeShareStation.Label}%7C{bikeShareStation.BikeShareStation.Latitude},{bikeShareStation.BikeShareStation.Longitude}"));
            var imageUrl = string.Format(baseImageUrl, userMarker, stationMarkers, routePolyline);
            return imageUrl;

            async Task<string> FindDetailedRouteToStation() {
                var bikeShareStation = nearestBikeStations[0].BikeShareStation;
                var queryString = string.Format(baseRouteQueryString, from, $"{bikeShareStation.Latitude},{bikeShareStation.Longitude}");
                var response = await httpClient.GetStringAsync(queryString);
                var route = JsonConvert.DeserializeObject<Route>(response);
                if (route.Status != "OK" || route.Routes.Length == 0) {
                    throw new InvalidOperationException($"Could not find a route from {from} to {bikeShareStation.Name}, {bikeShareStation.Address}.");
                }

                return route.Routes[0].OverviewPolyline.Points;
            }
        }
    }
}
