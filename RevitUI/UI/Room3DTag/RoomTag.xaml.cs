using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DataLab;
using RevitUI.ExternalCommand.Room3DTag;
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

namespace RevitUI.UI.Room3DTag
{
    /// <summary>
    /// Interaction logic for RoomTag.xaml
    /// </summary>
    public partial class RoomTag : Window
    {
        private Document doc;
        private UIDocument uidoc;
        private ExternalEvent MyExternalEvent;
        private EventHandler Room3DTagExternal;
        
        public RoomTag(Document doc, UIDocument uidoc)
        {
            InitializeComponent();
            this.doc = doc;
            this.uidoc = uidoc;
            MyExternalEvent = ExternalEvent.Create(new Room3DTagExternal());
        }

        public void info()
        {
            var phases = doc.GetPhasing();

        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            Room3DTagExternal = new EventHandler((s, args) =>
            {
                MyExternalEvent.Raise();
            });
        }

        private void BtnDlt_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
