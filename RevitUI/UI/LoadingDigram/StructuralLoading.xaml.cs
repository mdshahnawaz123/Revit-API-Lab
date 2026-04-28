using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DataLab;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace RevitUI.UI.LoadingDigram
{
    public class SelectionItemViewModel
    {
        public string Name { get; set; }
        public bool IsChecked { get; set; }
        public List<Element> Elements { get; set; }
    }

    /// <summary>
    /// Interaction logic for StructuralLoading.xaml
    /// </summary>
    public partial class StructuralLoading : Window
    {
        public Document doc;
        public UIDocument uidoc;

        private RevitUI.ExternalCommand.Loading.RoomLoadingExternal _roomCollectionHandler;
        private ExternalEvent _roomCollectionEvent;

        private RevitUI.ExternalCommand.Loading.WallCollectionHandler _wallCollectionHandler;
        private ExternalEvent _wallCollectionEvent;

        private RevitUI.ExternalCommand.Loading.WallLineStyleHandler _wallLineStyleHandler;
        private ExternalEvent _wallLineStyleEvent;

        private RevitUI.ExternalCommand.Loading.RoomFilledRegionHandler _roomFilledRegionHandler;
        private ExternalEvent _roomFilledRegionEvent;

        private RevitUI.ExternalCommand.Loading.LoadingLegendHandler _legendHandler;
        private ExternalEvent _legendEvent;

        public StructuralLoading(Document doc, UIDocument uidoc)
        {
            InitializeComponent();
            this.doc = doc;
            this.uidoc = uidoc;

            _roomCollectionHandler = new RevitUI.ExternalCommand.Loading.RoomLoadingExternal();
            _roomCollectionHandler.OnRoomsCollected = OnRoomsCollected;
            _roomCollectionEvent = ExternalEvent.Create(_roomCollectionHandler);

            _wallCollectionHandler = new RevitUI.ExternalCommand.Loading.WallCollectionHandler();
            _wallCollectionHandler.OnWallsCollected = OnWallsCollected;
            _wallCollectionEvent = ExternalEvent.Create(_wallCollectionHandler);

            _wallLineStyleHandler = new RevitUI.ExternalCommand.Loading.WallLineStyleHandler();
            _wallLineStyleEvent = ExternalEvent.Create(_wallLineStyleHandler);

            _roomFilledRegionHandler = new RevitUI.ExternalCommand.Loading.RoomFilledRegionHandler();
            _roomFilledRegionEvent = ExternalEvent.Create(_roomFilledRegionHandler);

            _legendHandler = new RevitUI.ExternalCommand.Loading.LoadingLegendHandler();
            _legendEvent = ExternalEvent.Create(_legendHandler);
        }

        private void RefreshRooms()
        {
            if (_roomCollectionHandler != null && _roomCollectionEvent != null)
            {
                _roomCollectionHandler.IsHostModelOption = HostModelOption.IsChecked == true;
                _roomCollectionEvent.Raise();
            }
        }

        private void OnRoomsCollected(List<SpatialElement> rooms)
        {
            var groupedRooms = rooms
                .GroupBy(r => r.Name)
                .Select(g => new SelectionItemViewModel
                {
                    Name = g.Key,
                    IsChecked = false,
                    Elements = g.Cast<Element>().ToList()
                })
                .OrderBy(x => x.Name)
                .ToList();

            Dispatcher.Invoke(() =>
            {
                RoomListBox.ItemsSource = groupedRooms;
            });
        }

        private void SyncRoom(object sender, RoutedEventArgs e)
        {
            RefreshRooms();
        }

        private void RefreshWalls()
        {
            if (_wallCollectionHandler != null && _wallCollectionEvent != null)
            {
                _wallCollectionHandler.IsHostModelOption = HostModelOption.IsChecked == true;
                _wallCollectionEvent.Raise();
            }
        }

        private void OnWallsCollected(List<Wall> walls)
        {
            var groupedWalls = walls
                .GroupBy(w => w.Name)
                .Select(g => new SelectionItemViewModel
                {
                    Name = g.Key,
                    IsChecked = false,
                    Elements = g.Cast<Element>().ToList()
                })
                .OrderBy(x => x.Name)
                .ToList();

            Dispatcher.Invoke(() =>
            {
                WallListBox.ItemsSource = groupedWalls;
            });
        }

        private void SyncWall(object sender, RoutedEventArgs e)
        {
            RefreshWalls();
        }

        private void CreateFilled(object sender, RoutedEventArgs e)
        {
            if (_roomFilledRegionHandler != null && _roomFilledRegionEvent != null)
            {
                var viewModels = RoomListBox.ItemsSource as List<SelectionItemViewModel>;
                if (viewModels != null)
                {
                    var selectedRooms = viewModels
                        .Where(vm => vm.IsChecked)
                        .SelectMany(vm => vm.Elements)
                        .ToList();

                    _roomFilledRegionHandler.SelectedRooms = selectedRooms;
                    _roomFilledRegionHandler.IsHostModelOption = HostModelOption.IsChecked == true;
                    _roomFilledRegionEvent.Raise();
                }
            }
        }

        private void CreateWallLine(object sender, RoutedEventArgs e)
        {
            if (_wallLineStyleHandler != null && _wallLineStyleEvent != null)
            {
                var viewModels = WallListBox.ItemsSource as List<SelectionItemViewModel>;
                if (viewModels != null)
                {
                    // Collect all Elements from items where IsChecked is true
                    var selectedWalls = viewModels
                        .Where(vm => vm.IsChecked)
                        .SelectMany(vm => vm.Elements)
                        .ToList();

                    _wallLineStyleHandler.SelectedWalls = selectedWalls;
                    _wallLineStyleEvent.Raise();
                }
            }
        }

        private void CreateLegend(object sender, RoutedEventArgs e)
        {
            if (_legendHandler != null && _legendEvent != null)
            {
                _legendEvent.Raise();
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1)
            {
                string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyDirectory = System.IO.Path.GetDirectoryName(assemblyLocation);
                string helpPath = System.IO.Path.Combine(assemblyDirectory, "Helper", "StructuralLoadingHelp.html");

                if (System.IO.File.Exists(helpPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = helpPath,
                        UseShellExecute = true
                    });
                }
                e.Handled = true;
            }
        }
    }
}
