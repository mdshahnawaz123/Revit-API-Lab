using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using DataLab;
using RevitUI.ExternalCommand.Room3DTag;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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

namespace RevitUI.UI.Room3DTag
{
    /// <summary>
    /// Interaction logic for RoomTag.xaml
    /// </summary>
    public partial class RoomTag : Window
    {
        // ── Win32 singleton helpers ───────────────────────────────────────────
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        private const int SW_RESTORE = 9;

        private Document doc;
        private UIDocument uidoc;
        private ExternalEvent externalEvent;
        private Room3DTagExternal room3DTagExternal;

        private ExistingTagDelete existingTagDelete;
        private ExternalEvent externalEventDelete;

        private SyncTagHandler syncTagHandler;
        private ExternalEvent externalEventSync;

        public ObservableCollection<RoomSourceModel> RoomSourceModels { get; set; }
            = new ObservableCollection<RoomSourceModel>();

        // ── Singleton factory ─────────────────────────────────────────────────
        public static void GetOrCreate(Document doc, UIDocument uidoc)
        {
            const string title = "BIM Digital Design";
            IntPtr hwnd = FindWindow(null, title);
            if (hwnd != IntPtr.Zero && IsWindow(hwnd))
            {
                ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
                return;
            }
            new RoomTag(doc, uidoc).Show();
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public RoomTag(Document doc, UIDocument uidoc)
        {
            InitializeComponent();
            this.HideIcon();
            this.doc = doc;
            this.uidoc = uidoc;
            room3DTagExternal = new Room3DTagExternal();
            externalEvent = ExternalEvent.Create(room3DTagExternal);
            existingTagDelete = new ExistingTagDelete();
            existingTagDelete.ActiveViewCheckBox = ActiveViewCheckBox;
            existingTagDelete.AllModelsCheckBox = AllModelsCheckBox;
            externalEventDelete = ExternalEvent.Create(existingTagDelete);
            syncTagHandler = new SyncTagHandler();
            externalEventSync = ExternalEvent.Create(syncTagHandler);
            info();
        }


        public void info()
        {
            // ── Collect Phases ───────────────────────────────────────────────
            var phases = new FilteredElementCollector(doc)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .ToList();
            PhaseComboBox.ItemsSource = phases;
            if (phases.Any()) PhaseComboBox.SelectedIndex = phases.Count - 1; 

            // ── Collect Worksets ─────────────────────────────────────────────
            var worksets = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToList();
            WorksetComboBox.ItemsSource = worksets;
            if (worksets.Any()) WorksetComboBox.SelectedIndex = 0;

            // ── Check if family is loaded ─────────────────────────────────────
            string familyName = "3D Room Tag (BDD)";
            Family family = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name == familyName);

            if (family == null)
            {
                // Try to load from embedded resources
                try
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    // Dynamically find the resource name to handle naming variations
                    string resourceName = assembly.GetManifestResourceNames()
                        .FirstOrDefault(n => n.EndsWith("3D Room Tag (BDD).rfa") || n.Contains("3D_Room_Tag"));

                    if (!string.IsNullOrEmpty(resourceName))
                    {
                        using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (stream != null)
                            {
                                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), familyName + ".rfa");
                                using (FileStream fileStream = File.Create(tempPath))
                                {
                                    stream.CopyTo(fileStream);
                                }

                                using (Transaction tx = new Transaction(doc, "Load Family"))
                                {
                                    tx.Start();
                                    doc.LoadFamily(tempPath, new JtFamilyLoadOptions(), out family);
                                    tx.Commit();
                                }
                                
                                try { File.Delete(tempPath); } catch { }
                            }
                        }
                    }
                }
                catch { /* Silent fail */ }
            }

            // ── Populate symbols ──────────────────────────────────────────────
            if (family != null)
            {
                var symbols = family.GetFamilySymbolIds()
                    .Select(id => doc.GetElement(id) as FamilySymbol)
                    .ToList();

                RoomTagTypeComboBox.ItemsSource = symbols;
                RoomTagTypeComboBox.SelectedItem = symbols.FirstOrDefault();
            }

            LoadRoomSourceModels();
        }

        private void OnDeleteOptionChanged(object sender, RoutedEventArgs e)
        {
            if (ActiveViewCheckBox == null || AllModelsCheckBox == null) return;

            if (sender == ActiveViewCheckBox && ActiveViewCheckBox.IsChecked == true)
                AllModelsCheckBox.IsChecked = false;
            else if (sender == AllModelsCheckBox && AllModelsCheckBox.IsChecked == true)
                ActiveViewCheckBox.IsChecked = false;
        }

        private void OnSourceModelChanged(object sender, RoutedEventArgs e)
        {
            if (ChkHostModel == null || ChkLinkedModel == null) return;

            // Both can be checked at the same time — no mutual exclusion
            LoadRoomSourceModels();
        }

        /// <summary>
        /// Collects rooms from the host document and any linked models,
        /// computes placement statistics, and populates the DataGrid.
        /// Respects the Host Model and Linked Model checkboxes.
        /// </summary>
        private void LoadRoomSourceModels()
        {
            // Guard: UI components might not be initialized yet during first call
            if (RoomSourceDataGrid == null || ChkHostModel == null || ChkLinkedModel == null) return;

            RoomSourceModels.Clear();

            // ── Host document rooms ──────────────────────────────────────────
            if (ChkHostModel.IsChecked == true)
            {
                AddRoomModelEntry(doc, "( Current Model )");
            }

            // ── Linked document rooms ────────────────────────────────────────
            if (ChkLinkedModel.IsChecked == true)
            {
                var links = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .WhereElementIsNotElementType()
                    .Cast<RevitLinkInstance>()
                    .ToList();

                foreach (var link in links)
                {
                    var linkDoc = link.GetLinkDocument();
                    if (linkDoc == null) continue;

                    var linkName = linkDoc.Title ?? link.Name;
                    AddRoomModelEntry(linkDoc, linkName);
                }
            }

            RoomSourceDataGrid.ItemsSource = RoomSourceModels;
        }

        private void OnSourceCheckBoxChanged(object sender, RoutedEventArgs e)
        {
            LoadRoomSourceModels();
        }

        /// <summary>
        /// Queries a single document for rooms, groups by phase, and adds
        /// one RoomSourceModel row per phase found.
        /// </summary>
        private void AddRoomModelEntry(Document targetDoc, string modelName)
        {
            var rooms = new FilteredElementCollector(targetDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<SpatialElement>()
                .OfType<Room>()
                .ToList();

            // Check volume calculation setting
            bool volumeCalcOn = false;
            try
            {
                var areaSchemes = new FilteredElementCollector(targetDoc)
                    .OfClass(typeof(AreaScheme))
                    .ToList();
                volumeCalcOn = areaSchemes.Any();
            }
            catch { /* not critical */ }

            // Group by phase
            var phaseGroups = rooms
                .GroupBy(r =>
                {
                    var phaseParam = r.get_Parameter(BuiltInParameter.ROOM_PHASE);
                    if (phaseParam == null) return ("Unknown", -1);
                    var phaseId = phaseParam.AsElementId();
                    var phase = targetDoc.GetElement(phaseId);
                    return (phase?.Name ?? "Unknown", phaseId?.Value ?? -1);
                })
                .ToList();

            if (!phaseGroups.Any())
            {
                // No rooms at all — still show the model with zeroes
                RoomSourceModels.Add(new RoomSourceModel
                {
                    IsSelected = false,
                    ModelName = modelName,
                    Category = "Rooms",
                    VolumeCalcOn = volumeCalcOn,
                    TotalRoomsSpaces = 0,
                    PlacedCorrectly = 0,
                    Redundant = 0,
                    NotPlaced = 0,
                    NotEnclosed = 0,
                    PhaseNumber = 0,
                    Phase = "",
                    DesignOption = ""
                });
                return;
            }

            int phaseIndex = 1;
            foreach (var group in phaseGroups)
            {
                var roomsInPhase = group.ToList();
                int total = roomsInPhase.Count;

                // Placed correctly = has area > 0 and location is valid
                int placed = roomsInPhase.Count(r => r.Area > 0 && r.Location != null);

                // Not Placed = no location at all
                int notPlaced = roomsInPhase.Count(r => r.Location == null);

                // Not Enclosed = has location but area == 0 (room is placed but boundaries are open)
                int notEnclosed = roomsInPhase.Count(r => r.Location != null && r.Area == 0);

                // Redundant = rooms that overlap / share same location (placed but redundant)
                int redundant = total - placed - notPlaced - notEnclosed;
                if (redundant < 0) redundant = 0;

                // Design option
                string designOpt = "";
                var firstRoom = roomsInPhase.FirstOrDefault();
                if (firstRoom != null)
                {
                    var doParam = firstRoom.get_Parameter(BuiltInParameter.DESIGN_OPTION_ID);
                    if (doParam != null)
                    {
                        var doId = doParam.AsElementId();
                        if (doId != null && doId != ElementId.InvalidElementId)
                        {
                            var doElem = targetDoc.GetElement(doId);
                            designOpt = doElem?.Name ?? "";
                        }
                    }
                }

                RoomSourceModels.Add(new RoomSourceModel
                {
                    IsSelected = false,
                    ModelName = modelName,
                    Category = "Rooms",
                    VolumeCalcOn = volumeCalcOn,
                    TotalRoomsSpaces = total,
                    PlacedCorrectly = placed,
                    Redundant = redundant,
                    NotPlaced = notPlaced,
                    NotEnclosed = notEnclosed,
                    PhaseNumber = phaseIndex,
                    Phase = group.Key.Item1,
                    DesignOption = designOpt
                });
                phaseIndex++;
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            // ── Pass UI selections to the handler ──────────────────────────
            // Phase
            if (PhaseComboBox.SelectedItem is Element selectedPhase)
                room3DTagExternal.SelectedPhaseId = selectedPhase.Id;
            else
                room3DTagExternal.SelectedPhaseId = ElementId.InvalidElementId;

            // Workset
            if (WorksetComboBox.SelectedItem is Workset selectedWorkset)
                room3DTagExternal.SelectedWorksetId = selectedWorkset.Id.IntegerValue;
            else
                room3DTagExternal.SelectedWorksetId = -1;

            // Update existing flag
            room3DTagExternal.UpdateExisting = ChkUpdateExisting.IsChecked == true;

            // Tag family type
            if (RoomTagTypeComboBox.SelectedItem is FamilySymbol selectedSymbol)
                room3DTagExternal.TagSymbolId = selectedSymbol.Id;
            else
                room3DTagExternal.TagSymbolId = ElementId.InvalidElementId;

            // Room Sources
            room3DTagExternal.IncludeHostModel = ChkHostModel.IsChecked == true;
            room3DTagExternal.IncludeLinkedModel = ChkLinkedModel.IsChecked == true;

            // Scope
            room3DTagExternal.ActiveViewOnly = ActiveViewCheckBox.IsChecked == true;

            externalEvent.Raise();
        }

        private void BtnDlt_Click(object sender, RoutedEventArgs e)
        {
            externalEventDelete.Raise();
        }

        private void BtnSync_Click(object sender, RoutedEventArgs e)
        {
            // Pass workset selection
            if (WorksetComboBox.SelectedItem is Workset selectedWorkset)
                syncTagHandler.SelectedWorksetId = selectedWorkset.Id.IntegerValue;
            else
                syncTagHandler.SelectedWorksetId = -1;

            // Pass scope: Active View or All Models
            syncTagHandler.ActiveViewOnly = ActiveViewCheckBox.IsChecked == true;

            externalEventSync.Raise();
        }
    }

    /// <summary>
    /// Simple helper to overwrite families if they already exist.
    /// </summary>
    public class JtFamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
