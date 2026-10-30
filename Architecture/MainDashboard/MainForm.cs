using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Architecture.GIS.UI;
using Architecture.GIS.Domain.Models;
using Architecture.GIS.Domain.Layers;

namespace Architecture
{
    // ==========================================
    // 5. Application Entry Point / Main Form
    // ==========================================
    public partial class MainForm : Form
    {
        public MainForm()
        {
            Text = "Enterprise Retail GIS Platform (USA / Global)";
            Size = new Size(1100, 700);
            StartPosition = FormStartPosition.CenterScreen;
            InitializeComponent();
        }
        // class-data:
        private GisMapControl _map;


        private void InitializeGisEngine()
        {
            _map = new GisMapControl
            {
                Dock = DockStyle.Fill,
                Zoom = 4,
                Center = new GeoCoordinate(37.0902, -95.7129) // USA Center
            };

            // Layer 1: Retail Chains (Walmart, Target)
            var retailLayer = new VectorLayer { Name = "Retail Networks" };
            retailLayer.Markers.Add(new MapMarker("Walmart Supercenter", new GeoCoordinate(40.7128, -74.0060), StoreType.Supermarket, Color.Blue)); // NY
            retailLayer.Markers.Add(new MapMarker("Target HQ", new GeoCoordinate(44.9778, -93.2650), StoreType.Retail, Color.Red)); // Minneapolis
            retailLayer.Markers.Add(new MapMarker("Walmart Hub", new GeoCoordinate(34.0522, -118.2437), StoreType.Supermarket, Color.Blue)); // LA

            // Layer 2: Food & Grocery Chains (Whole Foods, Trader Joe's)
            var groceryLayer = new VectorLayer { Name = "Grocery Stores" };
            groceryLayer.Markers.Add(new MapMarker("Trader Joe's", new GeoCoordinate(37.7749, -122.4194), StoreType.Grocery, Color.DarkGreen)); // SF
            groceryLayer.Markers.Add(new MapMarker("Whole Foods Market", new GeoCoordinate(30.2672, -97.7431), StoreType.Grocery, Color.Green)); // Austin, TX
            groceryLayer.Markers.Add(new MapMarker("Local Organic Food", new GeoCoordinate(41.8781, -87.6298), StoreType.Grocery, Color.Orange)); // Chicago

            _map.Layers.Add(retailLayer);
            _map.Layers.Add(groceryLayer);

            _map.MarkerClicked += (s, marker) =>
            {
                MessageBox.Show(
                    $"Store Selected:\nName: {marker.Name}\nType: {marker.Type}\nCoordinates: {marker.Location.Latitude:F4}, {marker.Location.Longitude:F4}",
                    "GIS Store Inspector",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };

            Controls.Add(_map);
        }
        
    }
}
