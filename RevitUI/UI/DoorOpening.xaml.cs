
using Autodesk.Revit.UI;
using RevitUI.ExternalCommand.Opening;
using System;
using System.Windows;
using System.Windows.Threading;

namespace RevitUI.UI
{
    public partial class DoorOpening : Window
    {
        private readonly ExternalEvent _scanEvent;
        private readonly LinkedDoorScanHandler _scanHandler;

        private readonly ExternalEvent _openingEvent;
        private readonly LinkedDoorWindowOpeningHandler _openingHandler;

        public DoorOpening(
            ExternalEvent scanEvent,
            LinkedDoorScanHandler scanHandler,
            ExternalEvent openingEvent,
            LinkedDoorWindowOpeningHandler openingHandler)
        {
            InitializeComponent();
            this.HideIcon();
            _scanEvent = scanEvent;
            _scanHandler = scanHandler;
            _openingEvent = openingEvent;
            _openingHandler = openingHandler;
        }

        // ── SCAN BUTTON ──────────────────────────────────────────────────────
        private void ScanBtn_Click(object sender, RoutedEventArgs e)
        {
            if (DoorCheckBox.IsChecked != true && WindowCheckBox.IsChecked != true)
            {
                MessageBox.Show("Please select at least Door or Window.", "Warning");
                return;
            }

            TxtStatus.Text = "Scanning...";
            OpeningDataGrid.ItemsSource = null;

            _scanHandler.DoorCheckBox = DoorCheckBox.IsChecked == true;
            _scanHandler.WindowCheckBox = WindowCheckBox.IsChecked == true;

            _scanEvent.Raise();

            // Poll until scan handler has results
            PollUntilComplete(
                isComplete: () => _scanHandler.IsComplete,
                onComplete: () =>
                {
                    OpeningDataGrid.ItemsSource = _scanHandler.Results;
                    TxtStatus.Text = _scanHandler.Results.Count > 0
                        ? $"Found {_scanHandler.Results.Count} element(s) — Ready to create openings"
                        : "No elements found. Check linked models.";
                    _scanHandler.IsComplete = false; // reset for next scan
                });
        }

        // ── CREATE OPENINGS BUTTON ───────────────────────────────────────────
        private void OpeningBtn(object sender, RoutedEventArgs e)
        {
            if (_scanHandler.Results == null || _scanHandler.Results.Count == 0)
            {
                MessageBox.Show("No elements found. Please scan first.", "Warning");
                return;
            }

            if (DoorCheckBox.IsChecked != true && WindowCheckBox.IsChecked != true)
            {
                MessageBox.Show("Please select at least Door or Window.", "Warning");
                return;
            }

            TxtStatus.Text = "Creating openings...";

            _openingHandler.DoorCheckBox = DoorCheckBox.IsChecked == true;
            _openingHandler.WindowCheckBox = WindowCheckBox.IsChecked == true;

            _openingEvent.Raise();

            // Poll until opening handler finishes
            PollUntilComplete(
                isComplete: () => _openingHandler.ResultMessage != "",
                onComplete: () =>
                {
                    TxtStatus.Text = _openingHandler.ResultMessage;

                    // Refresh status column in grid
                    OpeningDataGrid.ItemsSource = null;
                    OpeningDataGrid.ItemsSource = _scanHandler.Results;
                });
        }

        /// <summary>
        /// Polls every 200ms until isComplete() returns true, then fires onComplete() on the UI thread.
        /// Solves the async gap between ExternalEvent.Raise() and actual Revit execution.
        /// </summary>
        private void PollUntilComplete(Func<bool> isComplete, Action onComplete)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (s, args) =>
            {
                if (!isComplete()) return;
                timer.Stop();
                onComplete();
            };
            timer.Start();
        }
    }
}