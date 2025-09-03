using Syncfusion.Blazor.Charts;
using Syncfusion.Blazor.Inputs;
using Syncfusion.Blazor.Layouts;
using Syncfusion.Blazor.Maps;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AirQualityChartTracker.Components.Pages
{
    public partial class Home : INotifyPropertyChanged
    {
        #region Fields

        private string countryName = "New York";
        private ObservableCollection<AirQualityInfo>? data;
        private ObservableCollection<AirQualityInfo>? foreCastData;
        private ObservableCollection<AirQualityInfo>? mapMarkers;
        private string currentPollutionIndex = "Loading...";
        private string avgPollution7Days = "Loading...";
        private string aiPredictionAccuracy = "Loading...";
        private string latestAirQualityStatus = "Loading...";
        private Timer _resizeTimer;
        public bool IsInitialRender { get; set; }
        private bool SpinnerVisibility { get; set; } = true;
        private bool MapSpinnerVisibility { get; set; } = true;
        private bool ShowMarker { get; set; } = true;
        private string _searchCountry = "New York";
        private static System.Timers.Timer timer;
        SfChart chart1;
        SfDashboardLayout sfDashboardLayout;
        SfMaps MapsLayout;
        private bool isLayoutRender;

        private SfTextBox countryTextBox;

        #endregion

        #region Properties

        public string CountryName
        {
            get => countryName;
            set
            {
                countryName = value;
                OnPropertyChanged(nameof(CountryName));
            }
        }

        public ObservableCollection<AirQualityInfo>? Data
        {
            get => data;
            set
            {
                data = value;
                OnPropertyChanged(nameof(Data));
            }
        }

        public ObservableCollection<AirQualityInfo>? ForeCastData
        {
            get => foreCastData;
            set
            {
                foreCastData = value;
                OnPropertyChanged(nameof(ForeCastData));
            }
        }

        public ObservableCollection<AirQualityInfo>? MapMarkers
        {
            get => mapMarkers;
            set
            {
                mapMarkers = value;
                OnPropertyChanged(nameof(MapMarkers));
            }
        }

        public string CurrentPollutionIndex
        {
            get => currentPollutionIndex;
            set
            {
                if (currentPollutionIndex != value)
                {
                    currentPollutionIndex = value;
                    OnPropertyChanged(nameof(CurrentPollutionIndex));
                }
            }
        }

        public string AvgPollution7Days
        {
            get => avgPollution7Days;
            set
            {
                if (avgPollution7Days != value)
                {
                    avgPollution7Days = value;
                    OnPropertyChanged(nameof(AvgPollution7Days));
                }
            }
        }

        public string AIPredictionAccuracy
        {
            get => aiPredictionAccuracy;
            set
            {
                if (aiPredictionAccuracy != value)
                {
                    aiPredictionAccuracy = value;
                    OnPropertyChanged(nameof(AIPredictionAccuracy));
                }
            }
        }

        public string LatestAirQualityStatus
        {
            get => latestAirQualityStatus;
            set
            {
                if (latestAirQualityStatus != value)
                {
                    latestAirQualityStatus = value;
                    OnPropertyChanged(nameof(LatestAirQualityStatus));
                }
            }
        }
        public string SearchCountry
        {
            get => _searchCountry;
            set
            {
                _searchCountry = value;
                CountryName = value;
            }
        }


        #endregion

        #region Methods

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await FetchAirQualityData("New York");
                SpinnerVisibility = MapSpinnerVisibility = true;
                if (Data != null && sfDashboardLayout != null)
                {
                    SpinnerVisibility = MapSpinnerVisibility = false;
                    await sfDashboardLayout.RefreshAsync();
                }
            }
            await base.OnAfterRenderAsync(firstRender);
        }
        internal async Task FetchAirQualityData(string countryName)
        {

            var newData = await AIService.PredictAirQualityTrends(countryName);
            Data = new ObservableCollection<AirQualityInfo>(newData);

            var singleMarker = Data.Select(d => new AirQualityInfo
            {
                Latitude = d.Latitude,
                Longitude = d.Longitude
            }).FirstOrDefault();

            if (singleMarker != null)
            {
                MapMarkers = new ObservableCollection<AirQualityInfo> { singleMarker };
            }

            CountryName = countryName;

            UpdateCalculatedProperties(Data);

        }

        internal async Task PredictForecastData()
        {

            var historicalData = Data?.OrderByDescending(d => d.Date).Take(40)
                .Select(d => new AirQualityInfo
                {
                    Date = d.Date,
                    PollutionIndex = d.PollutionIndex
                })
                .ToList();

            if (historicalData != null)
            {
                var forecastedData = await AIService.PredictNextMonthForecast(historicalData);

                ForeCastData = new ObservableCollection<AirQualityInfo>(forecastedData);
                UpdateCalculatedProperties(ForeCastData);
            }
        }

        private void UpdateCalculatedProperties(ObservableCollection<AirQualityInfo> data)
        {

            var latestData = data?.OrderByDescending(d => d.Date).FirstOrDefault();
            CurrentPollutionIndex = latestData != null ? latestData.PollutionIndex.ToString("F0") : "0";

            var last7Days = data?.OrderByDescending(d => d.Date).Take(7).ToList();
            AvgPollution7Days = (last7Days != null && last7Days.Any())
                ? last7Days.Average(d => d.PollutionIndex).ToString("F2")
                : "0.00";

            AIPredictionAccuracy = (data != null && data.Any())
                ? Data.Average(d => d.AIPredictionAccuracy).ToString("F2")
                : "0.00";

            LatestAirQualityStatus = latestData?.AirQualityStatus ?? "Unknown";
        }
        public async void Created(Object args)
        {
            await Task.Yield();
            IsInitialRender = true;
            isLayoutRender = true;
        }

        public async Task ResizingWindow(ResizeArgs args)
        {
            if (_resizeTimer != null)
            {
                _resizeTimer.Dispose();
            }
            _resizeTimer = new Timer(async _ =>
            {
                await InvokeAsync(() =>
                {
                    RefreshComponents();
                });
            }, null, 500, Timeout.Infinite);
        }

        private async Task RefreshComponents()
        {
            await Task.Yield();
            MapsLayout.Refresh();
        }


        private async Task TextChanged(Microsoft.AspNetCore.Components.ChangeEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(SearchCountry))
            {
                ShowMarker = false;
                SpinnerVisibility = MapSpinnerVisibility = true;
                Data = new ObservableCollection<AirQualityInfo>();
                ForeCastData = new ObservableCollection<AirQualityInfo>();
                MapMarkers?.Clear();
                SearchCountry = e.Value?.ToString();

                // Show loading states
                CurrentPollutionIndex = "Loading...";
                AIPredictionAccuracy = "Loading...";
                AvgPollution7Days = "Loading...";
                LatestAirQualityStatus = "Loading...";
                await FetchAirQualityData(SearchCountry);
                if (Data != null && sfDashboardLayout != null)
                {
                    ShowMarker = true;
                    SpinnerVisibility = MapSpinnerVisibility = false;
                    await sfDashboardLayout.RefreshAsync();
                    await chart1.RefreshAsync();
                }
            }
        }

        private async void ForecastButton_Click()
        {
            if (!string.IsNullOrWhiteSpace(SearchCountry))
            {
                SpinnerVisibility = true;

                CurrentPollutionIndex = "Loading...";
                AIPredictionAccuracy = "Loading...";
                AvgPollution7Days = "Loading...";
                LatestAirQualityStatus = "Loading...";
                await PredictForecastData();
                if (ForeCastData != null && sfDashboardLayout != null)
                {
                    ShowMarker = true;
                    SpinnerVisibility = false;
                    await sfDashboardLayout.RefreshAsync();
                    await chart1.RefreshAsync();
                }
            }
        }

        #endregion

        #region Property Changed Event

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class AirQualityInfo
    {
        public DateTime Date { get; set; }
        public double PollutionIndex { get; set; }
        public string? AirQualityStatus { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double AIPredictionAccuracy { get; set; }
    }
}