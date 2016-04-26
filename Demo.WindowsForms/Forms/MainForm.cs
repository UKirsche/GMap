using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using Demo.WindowsForms.CustomMarkers;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using GMap.NET.WindowsForms.ToolTips;
using System.Reflection;

namespace Demo.WindowsForms
{
    public partial class MainForm : Form
    {
        // layers

        #region Members: top (oberste Ebene), objects (marker), routen, polygone -> liegen in WindowForms Projekt
        readonly GMapOverlay top = new GMapOverlay();
        internal readonly GMapOverlay objects = new GMapOverlay("objects");
        internal readonly GMapOverlay routes = new GMapOverlay("routes");
        internal readonly GMapOverlay polygons = new GMapOverlay("polygons");
        #endregion

        // marker
        GMapMarker currentMarker;

        // polygons
        GMapPolygon polygon;

        // etc
        readonly Random rnd = new Random();
        readonly DescendingComparer ComparerIpStatus = new DescendingComparer();
        GMapMarkerRect CurentRectMarker = null;
        string mobileGpsLog = string.Empty;
        bool isMouseDown = false;
        PointLatLng start;
        PointLatLng end;
        Stack<float> polygonDistance = new Stack<float>();
        SQLiteReferentenCache refCache = new SQLiteReferentenCache();
        DataTable dtReferenten;
        DataView dvReferenten;
        BindingSource dtBinding;

        public MainForm()
        {
            InitializeComponent();

            if (!GMapControl.IsDesignerHosted)
            {
                // add your custom map db provider
                //GMap.NET.CacheProviders.MySQLPureImageCache ch = new GMap.NET.CacheProviders.MySQLPureImageCache();
                //ch.ConnectionString = @"server=sql2008;User Id=trolis;Persist Security Info=True;database=gmapnetcache;password=trolis;";
                //MainMap.Manager.SecondaryCache = ch;

                // set your proxy here if need
                //GMapProvider.IsSocksProxy = true;
                //GMapProvider.WebProxy = new WebProxy("127.0.0.1", 1080);
                //GMapProvider.WebProxy.Credentials = new NetworkCredential("ogrenci@bilgeadam.com", "bilgeada");
                // or
                //GMapProvider.WebProxy = WebRequest.DefaultWebProxy;
                //                          

                // set cache mode only if no internet avaible
                if (!Stuff.PingNetwork("pingtest.com"))
                {
                    MainMap.Manager.Mode = AccessMode.CacheOnly;
                    MessageBox.Show("No internet connection available, going to CacheOnly mode.", "GMap.NET - Demo.WindowsForms", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }


                #region Karteninitialisierung: OSM, Position, Zoom
                // config map         
                // Sollte wohl OSM bleiben
                MainMap.MapProvider = GMapProviders.OpenStreetMap;

                //Trägt Start Koo-Ein! Passiert aber nicht von selber
                MainMap.Position = new PointLatLng(50.73743, 7.0982068);
                MainMap.MinZoom = 0;
                MainMap.MaxZoom = 24;
                MainMap.Zoom = 9;
                #endregion



                #region EventHandler: MouseMove, PostionChanged, TileLoad, ZoomChange, MarkerClick etc.
                //MainMap.ScaleMode = ScaleModes.Fractional;

                // map events
                {
                    MainMap.OnPositionChanged += new PositionChanged(MainMap_OnPositionChanged);

                    MainMap.OnTileLoadStart += new TileLoadStart(MainMap_OnTileLoadStart);
                    MainMap.OnTileLoadComplete += new TileLoadComplete(MainMap_OnTileLoadComplete);

                    MainMap.OnMapZoomChanged += new MapZoomChanged(MainMap_OnMapZoomChanged);

                    MainMap.OnMarkerClick += new MarkerClick(MainMap_OnMarkerClick);
                    MainMap.OnMarkerEnter += new MarkerEnter(MainMap_OnMarkerEnter);
                    MainMap.OnMarkerLeave += new MarkerLeave(MainMap_OnMarkerLeave);

                    MainMap.OnPolygonEnter += new PolygonEnter(MainMap_OnPolygonEnter);
                    MainMap.OnPolygonLeave += new PolygonLeave(MainMap_OnPolygonLeave);

                    MainMap.OnRouteEnter += new RouteEnter(MainMap_OnRouteEnter);
                    MainMap.OnRouteLeave += new RouteLeave(MainMap_OnRouteLeave);

                    MainMap.Manager.OnTileCacheComplete += new TileCacheComplete(OnTileCacheComplete);
                    MainMap.Manager.OnTileCacheStart += new TileCacheStart(OnTileCacheStart);
                    MainMap.Manager.OnTileCacheProgress += new TileCacheProgress(OnTileCacheProgress);
                }

                MainMap.MouseMove += new MouseEventHandler(MainMap_MouseMove);
                MainMap.MouseDown += new MouseEventHandler(MainMap_MouseDown);
                MainMap.MouseUp += new MouseEventHandler(MainMap_MouseUp);

                Umkreis.ItemClicked += new ToolStripItemClickedEventHandler(DrawCircle_Prepare);
;
                #endregion

                #region Select-Boxen KartenproviderListe
                // get map types
                //#if !MONO   // mono doesn't handle it, so we 'lost' provider list ;]
                //                comboBoxMapType.ValueMember = "Name";
                //                comboBoxMapType.DataSource = GMapProviders.List;
                //                comboBoxMapType.SelectedItem = MainMap.MapProvider;
                //#endif
                #endregion

                #region Zugriffsmodus auf Daten: Cache, Server, Beides
                // acccess mode
                //comboBoxMode.DataSource = Enum.GetValues(typeof(AccessMode));
                //comboBoxMode.SelectedItem = MainMap.Manager.Mode;
                #endregion

                #region Controller in Navi-Leiste initialisieren
                // get position

                // get cache modes
                //checkBoxUseRouteCache.Checked = MainMap.Manager.UseRouteCache;

                //MobileLogFrom.Value = DateTime.Today;
                //MobileLogTo.Value = DateTime.Now;

                // get zoom  
                trackBar1.Minimum = MainMap.MinZoom * 100;
                trackBar1.Maximum = MainMap.MaxZoom * 100;
                trackBar1.TickFrequency = 100;
                #endregion

#if DEBUG
                //Zeigt die Gridlines für den Debug-Mode an. Erstmal deaktiviert
                MainMap.ShowTileGridLines = false;
#endif

                ToolStripManager.Renderer = new BSE.Windows.Forms.Office2007Renderer();


                // perf
                timerPerf.Tick += new EventHandler(timer_Tick);


                #region MapOverlays setzen: Routen, Polygone, Objecte
                // add custom layers  
                {
                    MainMap.Overlays.Add(routes);
                    MainMap.Overlays.Add(polygons);
                    MainMap.Overlays.Add(objects);
                    MainMap.Overlays.Add(top);

                    routes.Routes.CollectionChanged += new GMap.NET.ObjectModel.NotifyCollectionChangedEventHandler(Routes_CollectionChanged);
                    objects.Markers.CollectionChanged += new GMap.NET.ObjectModel.NotifyCollectionChangedEventHandler(Markers_CollectionChanged);
                }
                #endregion

                #region SetzeMarker als Arrow
                // set current marker
                currentMarker = new GMarkerGoogle(MainMap.Position, GMarkerGoogleType.arrow);
                currentMarker.IsHitTestVisible = false;
                top.Markers.Add(currentMarker);
                #endregion

                #region Initialisiere Startposition als Marker myCity: GoogleMapApi-Abfrage
                //MainMap.VirtualSizeEnabled = true;
                //if(false)
                {
                    // add my city location for demo
                    GeoCoderStatusCode status = GeoCoderStatusCode.Unknow;
                    {
                        PointLatLng? pos = GMapProviders.GoogleMap.GetPoint("Germany, Bonn", out status);
                        if (pos != null && status == GeoCoderStatusCode.G_GEO_SUCCESS)
                        {
                            currentMarker.Position = pos.Value;

                            GMapMarker myCity = new GMarkerGoogle(pos.Value, GMarkerGoogleType.green_small);
                            myCity.ToolTipMode = MarkerTooltipMode.Always;
                            myCity.ToolTipText = "DVV";
                            objects.Markers.Add(myCity);
                        }
                    }
                    #endregion

                    //Initialisierungen nach Wegnahme von Controls
                    currentMarker.IsVisible = true;
                    MainMap.CanDragMap = true;

                    //Teste SQLiteReferenten
                    refCache.CacheLocation = MainMap.CacheLocation;
                    refCache.AddGeoInfoToReferents();
                    List<GMapMarker> listReferentMarkers = refCache.ReturnReferentsAsGMapMarker();
                    
                    foreach (var item in listReferentMarkers)
                    {
                        objects.Markers.Add(item);
                    }


                    //Referenten-Daten
                    dtReferenten = refCache.GetAllDataFromCache();
                    dvReferenten = new DataView(dtReferenten);

                    InitializeReferentenView();

                    //if (objects.Markers.Count > 0)
                    //{
                    //    MainMap.ZoomAndCenterMarkers(null);
                    //}

                    RegeneratePolygon();

                    #region Ende Initialisierung
                    try
                    {
                        GMapOverlay overlay = DeepClone<GMapOverlay>(objects);
                        Debug.WriteLine("ISerializable status for markers: OK");

                        GMapOverlay overlay2 = DeepClone<GMapOverlay>(polygons);
                        Debug.WriteLine("ISerializable status for polygons: OK");

                        GMapOverlay overlay3 = DeepClone<GMapOverlay>(routes);
                        Debug.WriteLine("ISerializable status for routes: OK");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("ISerializable failure: " + ex.Message);

#if DEBUG
                        if (Debugger.IsAttached)
                        {
                            Debugger.Break();
                        }
#endif
                    }
                    #endregion
                }
            }
        }





        private void InitializeReferentenView()
        {
      
            //dtBinding.DataSource = dtReferenten;
            dataGridViewReferenten.DataSource = dvReferenten;
            dataGridViewReferenten.Columns["fname"].Visible = false;
            dataGridViewReferenten.Columns["address"].Visible = false;
            dataGridViewReferenten.Columns["competence"].Visible = false;
            dataGridViewReferenten.Columns["region"].Visible = false;
            dataGridViewReferenten.Columns["lat"].Visible = false;
            dataGridViewReferenten.Columns["lng"].Visible = false;
            dataGridViewReferenten.Columns["name"].HeaderText = "Name";
            dataGridViewReferenten.Columns["place"].HeaderText = "Ort";

            dataGridViewReferenten.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader);
        }


        public T DeepClone<T>(T obj)
        {
            using (var ms = new System.IO.MemoryStream())
            {
                var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();

                formatter.Serialize(ms, obj);

                ms.Position = 0;

                return (T)formatter.Deserialize(ms);
            }
        }

        void Markers_CollectionChanged(object sender, GMap.NET.ObjectModel.NotifyCollectionChangedEventArgs e)
        {
            //textBoxMarkerCount.Text = objects.Markers.Count.ToString();
        }

        void Routes_CollectionChanged(object sender, GMap.NET.ObjectModel.NotifyCollectionChangedEventArgs e)
        {
            //textBoxrouteCount.Text = routes.Routes.Count.ToString();
        }

        #region -- performance test --

        double NextDouble(Random rng, double min, double max)
        {
            return min + (rng.NextDouble() * (max - min));
        }

        int tt = 0;
        void timer_Tick(object sender, EventArgs e)
        {
            var pos = new PointLatLng(NextDouble(rnd, MainMap.ViewArea.Top, MainMap.ViewArea.Bottom), NextDouble(rnd, MainMap.ViewArea.Left, MainMap.ViewArea.Right));
            GMapMarker m = new GMarkerGoogle(pos, GMarkerGoogleType.green_pushpin);
            {
                m.ToolTipText = (tt++).ToString();
                m.ToolTipMode = MarkerTooltipMode.Always;
            }

            objects.Markers.Add(m);

            if (tt >= 333)
            {
                timerPerf.Stop();
                tt = 0;
            }
        }

        System.Windows.Forms.Timer timerPerf = new System.Windows.Forms.Timer();
        #endregion

        Color GetRandomColor()
        {
            byte r = Convert.ToByte(rnd.Next(0, 111));
            byte g = Convert.ToByte(rnd.Next(0, 111));
            byte b = Convert.ToByte(rnd.Next(0, 111));

            return Color.FromArgb(144, r, g, b);
        }

        #region -- some functions --

        void RegeneratePolygon()
        {
            List<PointLatLng> polygonPoints = new List<PointLatLng>();

            foreach (GMapMarker m in objects.Markers)
            {
                if (m is GMapMarkerRect)
                {
                    m.Tag = polygonPoints.Count;
                    polygonPoints.Add(m.Position);
                }
            }

            if (polygon == null)
            {
                polygon = new GMapPolygon(polygonPoints, "polygon test");
                polygon.IsHitTestVisible = true;
                polygons.Polygons.Add(polygon);
            }
            else
            {
                polygon.Points.Clear();
                polygon.Points.AddRange(polygonPoints);

                if (polygons.Polygons.Count == 0)
                {
                    polygons.Polygons.Add(polygon);
                }
                else
                {
                    MainMap.UpdatePolygonLocalPosition(polygon);
                }
            }
        }

        /// <summary>
        /// adds marker using geocoder. Grober Hack!! Achtung
        /// </summary>
        /// <param name="place"></param>
        void AddLocationGermany(string place, string name)
        {
            GeoCoderStatusCode status = GeoCoderStatusCode.Unknow;
            PointLatLng? pos = GMapProviders.GoogleMap.GetPoint("Germany, " + place, out status);
            if (pos != null && status == GeoCoderStatusCode.G_GEO_SUCCESS)
            {
                GMarkerGoogle m = new GMarkerGoogle(pos.Value, GMarkerGoogleType.green);
                m.ToolTip = new GMapRoundedToolTip(m);
                m.ToolTipText = name;
                objects.Markers.Add(m);
            }
        }



        #endregion

        #region -- map events --

        void OnTileCacheComplete()
        {
            Debug.WriteLine("OnTileCacheComplete");
            long size = 0;
            int db = 0;
            try
            {
                DirectoryInfo di = new DirectoryInfo(MainMap.CacheLocation);
                var dbs = di.GetFiles("*.gmdb", SearchOption.AllDirectories);
                foreach (var d in dbs)
                {
                    size += d.Length;
                    db++;
                }
            }
            catch
            {
            }

            if (!IsDisposed)
            {
                MethodInvoker m = delegate
                {
                    //textBoxCacheSize.Text = string.Format(CultureInfo.InvariantCulture, "{0} db in {1:00} MB", db, size / (1024.0 * 1024.0));
                    //textBoxCacheStatus.Text = "all tiles saved!";
                };

                if (!IsDisposed)
                {
                    try
                    {
                        Invoke(m);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        void OnTileCacheStart()
        {
            Debug.WriteLine("OnTileCacheStart");

            if (!IsDisposed)
            {
                MethodInvoker m = delegate
                {
                    //textBoxCacheStatus.Text = "saving tiles...";
                };
                Invoke(m);
            }
        }

        void OnTileCacheProgress(int left)
        {
            if (!IsDisposed)
            {
                MethodInvoker m = delegate
                {
                    //textBoxCacheStatus.Text = left + " tile to save...";
                };
                Invoke(m);
            }
        }

        void MainMap_OnMarkerLeave(GMapMarker item)
        {
            if (item is GMapMarkerRect)
            {
                CurentRectMarker = null;

                GMapMarkerRect rc = item as GMapMarkerRect;
                rc.Pen.Color = Color.Blue;

                Debug.WriteLine("OnMarkerLeave: " + item.Position);
            }
        }

        void MainMap_OnMarkerEnter(GMapMarker item)
        {
            if (item is GMapMarkerRect)
            {
                GMapMarkerRect rc = item as GMapMarkerRect;
                rc.Pen.Color = Color.Red;

                CurentRectMarker = rc;
            }
            Debug.WriteLine("OnMarkerEnter: " + item.Position);
        }

        GMapPolygon currentPolygon = null;
        void MainMap_OnPolygonLeave(GMapPolygon item)
        {
            currentPolygon = null;
            item.Stroke.Color = Color.MidnightBlue;
            Debug.WriteLine("OnPolygonLeave: " + item.Name);
        }

        void MainMap_OnPolygonEnter(GMapPolygon item)
        {
            currentPolygon = item;
            item.Stroke.Color = Color.Red;
            Debug.WriteLine("OnPolygonEnter: " + item.Name);
        }

        GMapRoute currentRoute = null;
        void MainMap_OnRouteLeave(GMapRoute item)
        {
            currentRoute = null;
            item.Stroke.Color = Color.MidnightBlue;
            Debug.WriteLine("OnRouteLeave: " + item.Name);
        }

        void MainMap_OnRouteEnter(GMapRoute item)
        {
            currentRoute = item;
            item.Stroke.Color = Color.Red;
            Debug.WriteLine("OnRouteEnter: " + item.Name);
        }

        void MainMap_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isMouseDown = false;
            }
        }
          
        void MainMap_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isMouseDown = true;

                if (currentMarker.IsVisible)
                {
                    currentMarker.Position = MainMap.FromLocalToLatLng(e.X, e.Y);

                    var px = MainMap.MapProvider.Projection.FromLatLngToPixel(currentMarker.Position.Lat, currentMarker.Position.Lng, (int)MainMap.Zoom);
                    var tile = MainMap.MapProvider.Projection.FromPixelToTileXY(px);

                    Debug.WriteLine("MouseDown: geo: " + currentMarker.Position + " | px: " + px + " | tile: " + tile);
                }
            }

            RemoveCircles();
         }

        // move current marker with left holding
        void MainMap_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && isMouseDown)
            {
                if (CurentRectMarker == null)
                {
                    if (currentMarker.IsVisible)
                    {
                        currentMarker.Position = MainMap.FromLocalToLatLng(e.X, e.Y);
                    }
                }
                else // move rect marker
                {
                    PointLatLng pnew = MainMap.FromLocalToLatLng(e.X, e.Y);

                    int? pIndex = (int?)CurentRectMarker.Tag;
                    if (pIndex.HasValue)
                    {
                        if (pIndex < polygon.Points.Count)
                        {
                            polygon.Points[pIndex.Value] = pnew;
                            MainMap.UpdatePolygonLocalPosition(polygon);
                        }
                    }

                    if (currentMarker.IsVisible)
                    {
                        currentMarker.Position = pnew;
                    }
                    CurentRectMarker.Position = pnew;

                    if (CurentRectMarker.InnerMarker != null)
                    {
                        CurentRectMarker.InnerMarker.Position = pnew;
                    }
                }

                MainMap.Refresh(); // force instant invalidation
            }
        }

        // MapZoomChanged
        void MainMap_OnMapZoomChanged()
        {
            trackBar1.Value = (int)(MainMap.Zoom * 100.0);
            //textBoxZoomCurrent.Text = MainMap.Zoom.ToString();
        }

        // click on some marker
        void MainMap_OnMarkerClick(GMapMarker item, MouseEventArgs e)
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                if (item is GMapMarkerRect)
                {
                    GeoCoderStatusCode status;
                    var pos = GMapProviders.GoogleMap.GetPlacemark(item.Position, out status);
                    if (status == GeoCoderStatusCode.G_GEO_SUCCESS && pos != null)
                    {
                        GMapMarkerRect v = item as GMapMarkerRect;
                        {
                            v.ToolTipText = pos.Value.Address;
                        }
                        MainMap.Invalidate(false);
                    }
                }
                //else
                //{
                //    if (item.Tag != null)
                //    {
                //        if (currentTransport != null)
                //        {
                //            currentTransport.ToolTipMode = MarkerTooltipMode.OnMouseOver;
                //            currentTransport = null;
                //        }
                //        currentTransport = item;
                //        currentTransport.ToolTipMode = MarkerTooltipMode.Always;
                //    }
                //}
            }
        }

        // loader start loading tiles
        void MainMap_OnTileLoadStart()
        {
            MethodInvoker m = delegate ()
            {
                panelMenu.Text = "Menu: loading tiles...";
            };
            try
            {
                BeginInvoke(m);
            }
            catch
            {
            }
        }

        // loader end loading tiles
        void MainMap_OnTileLoadComplete(long ElapsedMilliseconds)
        {
            MainMap.ElapsedMilliseconds = ElapsedMilliseconds;

            MethodInvoker m = delegate ()
            {
                panelMenu.Text = "Menu, last load in " + MainMap.ElapsedMilliseconds + "ms";

                //textBoxMemory.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.00} MB of {1:0.00} MB", MainMap.Manager.MemoryCache.Size, MainMap.Manager.MemoryCache.Capacity);
            };
            try
            {
                BeginInvoke(m);
            }
            catch
            {
            }
        }

        // current point changed
        void MainMap_OnPositionChanged(PointLatLng point)
        {
            //textBoxLatCurrent.Text = point.Lat.ToString(CultureInfo.InvariantCulture);
            //textBoxLngCurrent.Text = point.Lng.ToString(CultureInfo.InvariantCulture);
        }

        PointLatLng lastPosition;
        int lastZoom;

        // center markers on start
        private void MainForm_Load(object sender, EventArgs e)
        {
            trackBar1.Value = (int)MainMap.Zoom * 100;
            Activate();
            TopMost = true;
            TopMost = false;
        }
        #endregion

        #region -- menu panels expanders --
        private void xPanderPanel1_Click(object sender, EventArgs e)
        {
            xPanderPanelList1.Expand(xPanderPanelMain);
        }

        #endregion

        #region -- ui events --

        bool UserAcceptedLicenseOnce = false;

        // change map type
        //private void comboBoxMapType_DropDownClosed(object sender, EventArgs e)
        //{
        //    if (!UserAcceptedLicenseOnce)
        //    {
        //        if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "License.txt"))
        //        {
        //            string ctn = File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + Path.DirectorySeparatorChar + "License.txt");
        //            int li = ctn.IndexOf("License");
        //            string txt = ctn.Substring(li);

        //            var d = new Demo.WindowsForms.Forms.Message();
        //            d.richTextBox1.Text = txt;

        //            if (DialogResult.Yes == d.ShowDialog())
        //            {
        //                UserAcceptedLicenseOnce = true;
        //                this.Text += " - license accepted by " + Environment.UserName + " at " + DateTime.Now;
        //            }
        //        }
        //        else
        //        {
        //            // user deleted License.txt ;}
        //            UserAcceptedLicenseOnce = true;
        //        }
        //    }

        //    if (UserAcceptedLicenseOnce)
        //    {
        //        MainMap.MapProvider = comboBoxMapType.SelectedItem as GMapProvider;
        //    }
        //    else
        //    {
        //        MainMap.MapProvider = GMapProviders.OpenStreetMap;
        //        comboBoxMapType.SelectedItem = MainMap.MapProvider;
        //    }
        //}

        //// change mdoe
        //private void comboBoxMode_DropDownClosed(object sender, EventArgs e)
        //{
        //    MainMap.Manager.Mode = (AccessMode)comboBoxMode.SelectedValue;
        //    MainMap.ReloadMap();
        //}

        // zoom
        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            MainMap.Zoom = trackBar1.Value / 100.0;
        }

        // go to
        private void button8_Click(object sender, EventArgs e)
        {
            GeoCoderStatusCode status = GeoCoderStatusCode.Unknow;
            PointLatLng? pos = GMapProviders.GoogleMap.GetPoint("Germany, " + textBoxGeo.Text, out status);
            if (pos != null && status == GeoCoderStatusCode.G_GEO_SUCCESS)
            {
                currentMarker.Position = (PointLatLng)pos;
                MainMap.Position = (PointLatLng)pos;

                var px = MainMap.MapProvider.Projection.FromLatLngToPixel(currentMarker.Position.Lat, currentMarker.Position.Lng, (int)MainMap.Zoom);
                var tile = MainMap.MapProvider.Projection.FromPixelToTileXY(px);
            }
        }

        // goto by geocoder
        private void textBoxGeo_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((Keys)e.KeyChar == Keys.Enter)
            {
                GeoCoderStatusCode status = MainMap.SetPositionByKeywords(textBoxGeo.Text);
                if (status != GeoCoderStatusCode.G_GEO_SUCCESS)
                {
                    MessageBox.Show("Geocoder can't find: '" + textBoxGeo.Text + "', reason: " + status.ToString(), "GMap.NET", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
        }

        // reload map
        private void button1_Click(object sender, EventArgs e)
        {
            MainMap.ReloadMap();
        }

        // cache config
        //private void checkBoxUseCache_CheckedChanged(object sender, EventArgs e)
        //{
        //    MainMap.Manager.UseRouteCache = checkBoxUseRouteCache.Checked;
        //    MainMap.Manager.UseGeocoderCache = checkBoxUseRouteCache.Checked;
        //    MainMap.Manager.UsePlacemarkCache = checkBoxUseRouteCache.Checked;
        //    MainMap.Manager.UseDirectionsCache = checkBoxUseRouteCache.Checked;
        //}

        // clear cache
        private void button2_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Are You sure?", "Clear GMap.NET cache?", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK)
            {
                try
                {
                    MainMap.Manager.PrimaryCache.DeleteOlderThan(DateTime.Now, null);
                    MessageBox.Show("Done. Cache is clear.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        // add test route -Y zechen route
        //private void button3_Click(object sender, EventArgs e)
        //{
        //    RoutingProvider rp = MainMap.MapProvider as RoutingProvider;
        //    if (rp == null)
        //    {
        //        rp = GMapProviders.OpenStreetMap; // use OpenStreetMap if provider does not implement routing
        //    }

        //    MapRoute route = rp.GetRoute(start, end, false, false, (int)MainMap.Zoom);
        //    if (route != null)
        //    {
        //        // add route
        //        GMapRoute r = new GMapRoute(route.Points, route.Name);
        //        r.IsHitTestVisible = true;
        //        routes.Routes.Add(r);

        //        // add route start/end marks
        //        GMapMarker m1 = new GMarkerGoogle(start, GMarkerGoogleType.green_big_go);
        //        m1.ToolTipText = "Start: " + route.Name;
        //        m1.ToolTipMode = MarkerTooltipMode.Always;

        //        GMapMarker m2 = new GMarkerGoogle(end, GMarkerGoogleType.red_big_stop);
        //        m2.ToolTipText = "End: " + end.ToString();
        //        m2.ToolTipMode = MarkerTooltipMode.Always;

        //        objects.Markers.Add(m1);
        //        objects.Markers.Add(m2);

        //        MainMap.ZoomAndCenterRoute(r);
        //    }
        //}

        // add marker on current position
        private void button4_Click(object sender, EventArgs e)
        {
            GMarkerGoogle m = new GMarkerGoogle(currentMarker.Position, GMarkerGoogleType.green_pushpin);
            GMapMarkerRect mBorders = new GMapMarkerRect(currentMarker.Position);
            {
                mBorders.InnerMarker = m;
                if (polygon != null)
                {
                    mBorders.Tag = polygon.Points.Count;
                }
                mBorders.ToolTipMode = MarkerTooltipMode.Always;
            }

            Placemark? p = null;
            GeoCoderStatusCode status;
            var ret = GMapProviders.GoogleMap.GetPlacemark(currentMarker.Position, out status);
            if (status == GeoCoderStatusCode.G_GEO_SUCCESS && ret != null)
            {
                p = ret;
            }

            if (p != null)
            {
                mBorders.ToolTipText = p.Value.Address;
            }
            else
            {
                mBorders.ToolTipText = currentMarker.Position.ToString();
            }

            objects.Markers.Add(m);
            objects.Markers.Add(mBorders);

            RegeneratePolygon();

            //Now the Polygons are updated lets update the path length!
            AddPathLength();
        }

        // clear routes
        private void button6_Click(object sender, EventArgs e)
        {
            routes.Routes.Clear();
        }

        // clear polygons
        private void button15_Click(object sender, EventArgs e)
        {
            polygons.Polygons.Clear();
        }

        // clear markers
        private void button5_Click(object sender, EventArgs e)
        {
            objects.Markers.Clear();
        }


        // can drag
        //private void checkBoxCanDrag_CheckedChanged(object sender, EventArgs e)
        //{
        //    MainMap.CanDragMap = checkBoxCanDrag.Checked;
        //}

        // set route start
        //private void buttonSetStart_Click(object sender, EventArgs e)
        //{
        //    start = currentMarker.Position;
        //}

        // set route end
        //private void buttonSetEnd_Click(object sender, EventArgs e)
        //{
        //    end = currentMarker.Position;
        //}

        // zoom to max for markers
        private void button7_Click(object sender, EventArgs e)
        {
            MainMap.ZoomAndCenterMarkers("objects");
        }



        // saves current map view 
        private void button12_Click(object sender, EventArgs e)
        {
            try
            {
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PNG (*.png)|*.png";
                    sfd.FileName = "DVV image";

                    Image tmpImage = MainMap.ToImage();
                    if (tmpImage != null)
                    {
                        using (tmpImage)
                        {
                            if (sfd.ShowDialog() == DialogResult.OK)
                            {
                                tmpImage.Save(sfd.FileName);

                                MessageBox.Show("Image saved: " + sfd.FileName, "GMap.NET", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Image failed to save: " + ex.Message, "GMap.NET", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // launch static map maker
        //private void button13_Click(object sender, EventArgs e)
        //{
        //    StaticImage st = new StaticImage(this);
        //    st.Owner = this;
        //    st.Show();
        //}


        // key-up events
        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            int offset = -22;

            if (e.KeyCode == Keys.Left)
            {
                MainMap.Offset(-offset, 0);
            }
            else if (e.KeyCode == Keys.Right)
            {
                MainMap.Offset(offset, 0);
            }
            else if (e.KeyCode == Keys.Up)
            {
                MainMap.Offset(0, -offset);
            }
            else if (e.KeyCode == Keys.Down)
            {
                MainMap.Offset(0, offset);
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (currentPolygon != null)
                {
                    polygons.Polygons.Remove(currentPolygon);
                    currentPolygon = null;
                }

                if (currentRoute != null)
                {
                    routes.Routes.Remove(currentRoute);
                    currentRoute = null;
                }

                if (CurentRectMarker != null)
                {
                    objects.Markers.Remove(CurentRectMarker);

                    if (CurentRectMarker.InnerMarker != null)
                    {
                        objects.Markers.Remove(CurentRectMarker.InnerMarker);
                    }
                    CurentRectMarker = null;

                    RegeneratePolygon();
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                MainMap.Bearing = 0;

                //if (currentTransport != null && !MainMap.IsMouseOverMarker)
                //{
                //    currentTransport.ToolTipMode = MarkerTooltipMode.OnMouseOver;
                //    currentTransport = null;
                //}
            }
        }

        // key-press events
        private void MainForm_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (MainMap.Focused)
            {
                if (e.KeyChar == '+')
                {
                    MainMap.Zoom = ((int)MainMap.Zoom) + 1;
                }
                else if (e.KeyChar == '-')
                {
                    MainMap.Zoom = ((int)(MainMap.Zoom + 0.99)) - 1;
                }
                else if (e.KeyChar == 'a')
                {
                    MainMap.Bearing--;
                }
                else if (e.KeyChar == 'z')
                {
                    MainMap.Bearing++;
                }
            }
        }

        private void buttonZoomUp_Click(object sender, EventArgs e)
        {
            MainMap.Zoom = ((int)MainMap.Zoom) + 1;
        }

        private void buttonZoomDown_Click(object sender, EventArgs e)
        {
            MainMap.Zoom = ((int)(MainMap.Zoom + 0.99)) - 1;
        }





        // open disk cache location
        //private void button17_Click(object sender, EventArgs e)
        //{
        //    try
        //    {
        //        string argument = "/select, \"" + MainMap.CacheLocation + "TileDBv5\"";
        //        System.Diagnostics.Process.Start("explorer.exe", argument);
        //    }
        //    catch (Exception ex)
        //    {
        //        MessageBox.Show("Failed to open: " + ex.Message, "GMap.NET", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //}

        // http://leafletjs.com/
        // Leaflet is a modern open-source JavaScript library for mobile-friendly interactive maps
        // Leaflet is designed with simplicity, performance and usability in mind. It works efficiently across all major desktop and mobile platforms out of the box
        //private void checkBoxTileHost_CheckedChanged(object sender, EventArgs e)
        //{
        //    if (checkBoxTileHost.Checked)
        //    {
        //        try
        //        {
        //            MainMap.Manager.EnableTileHost(8844);
        //            TryExtractLeafletjs();
        //        }
        //        catch (Exception ex)
        //        {
        //            MessageBox.Show("EnableTileHost: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //        }
        //    }
        //    else
        //    {
        //        MainMap.Manager.DisableTileHost();
        //    }
        //}

        #endregion

        /// <summary>
        /// Setze die letzte Stecknadel zurück:
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click_1(object sender, EventArgs e)
        {
            if(polygon.Points !=null && (polygon.Points.Count > 0))
            {
                //ComputePathLength
                SubstractPathLength();

                //Release last two markers (innner, and border)
                int markerLength = objects.Markers.Count;

                //Funktioniert da es ObservableCols sind
                objects.Markers.RemoveAt(markerLength - 1);
                objects.Markers.RemoveAt(markerLength - 2);

                //Remove last polygon-point:
                polygon.Points.RemoveAt(polygon.Points.Count - 1);
                //Redraw
                MainMap.UpdatePolygonLocalPosition(polygon);
                //Upda
                GC.Collect();
            }
        }

        /// <summary>
        /// addierd neuen pfad zur pfadlänge
        /// </summary>
        private void AddPathLength()
        {
            //Berechne neues Pfadlängen Delta
            int numberPoints = polygon.Points.Count;
            if (numberPoints > 1)
            {
                PointLatLng p1 = polygon.Points[numberPoints - 1];
                PointLatLng p2 = polygon.Points[numberPoints - 2];
                float resultKM = Convert.ToSingle(DistanceKiloMeters(p1, p2));
                polygonDistance.Push(resultKM);
            }

            //Addiere Polygone
            SumPolygonPath();

        }

        /// <summary>
        /// Addiert den gesamten Polygonpfad auf
        /// </summary>
        /// <returns></returns>
        private void SumPolygonPath()
        {
            float sumUp = 0.0f;

            foreach (var item in polygonDistance)
            {
                sumUp += item;
            }

            MainMap.Distance = sumUp;
        }

        /// <summary>
        /// Subtrahiert pfadlänge um den letzten pfad
        /// </summary>
        private void SubstractPathLength()
        {
            if (polygonDistance.Count > 0)
            {
                polygonDistance.Pop();
            }

            //Addiere
            SumPolygonPath();
        }

        /// <summary>
        /// Haversine formula: To compute shperical distance between tw points
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        private double DistanceKiloMeters(PointLatLng p1, PointLatLng p2)
        {
                long R = 6378137; // Earth’s mean radius in meter
                double dLat = Rad(p2.lat - p1.lat);
                double dLong = Rad(p2.lng - p1.lng);
                double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(Rad(p1.lat)) * Math.Cos(Rad(p2.lat)) *
                  Math.Sin(dLong / 2) * Math.Sin(dLong / 2);
                double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                double d = R * c;
                d = Math.Round(d);
                return d/1000; // returns the distance in kilo-meter

        }

        private double Rad(double x)
        {
            return x * Math.PI / 180;

        }


        private void RemoveCircles()
        {
            foreach (var item in objects.Markers)
            {
                if (item is GMapMarkerCircle)
                {
                    objects.Markers.Remove(item);
                    break;
                }
            }
        }


        private void DrawCircle_Prepare(object sender, ToolStripItemClickedEventArgs e)
        {
            int radius =  Convert.ToInt32(e.ClickedItem.Text)*1000; //Radius in Metern
            DrawCircle(radius);


        }

        // add demo circle
        private void DrawCircle(int radius)
        {
          
            var px = MainMap.MapProvider.Projection.FromLatLngToPixel(currentMarker.Position.Lat, currentMarker.Position.Lng, (int)MainMap.Zoom);
            var tile = MainMap.MapProvider.Projection.FromPixelToTileXY(px);

            var cc = new GMapMarkerCircle(currentMarker.Position, radius);
            objects.Markers.Add(cc);
        }

        private void MainMap_Load(object sender, EventArgs e)
        {

        }


        /// <summary>
        /// Filter die Tabelle nach Name und Adresse und Ort
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonFilter_Click(object sender, EventArgs e)
        {
            dvReferenten.RowFilter = "name LIKE '%" + textFilter.Text + "%' OR address LIKE '%" + textFilter.Text + "%' OR place LIKE '%" + textFilter.Text + "%'";
        }

        /// <summary>
        /// Setze Filter zurück
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonResetFilter_Click(object sender, EventArgs e)
        {
            dvReferenten.RowFilter = "name LIKE '%%'";
        }

        /// <summary>
        /// Zentriere Karte auf Auswahl des Referenten
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dataGridViewReferenten_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridViewReferenten.SelectedRows.Count > 0)
            {
                DataGridViewRow row = dataGridViewReferenten.SelectedRows[0];


                double lat = Convert.ToDouble(row.Cells["lat"].Value);
                double lng = Convert.ToDouble(row.Cells["lng"].Value);

                PointLatLng? pos = new PointLatLng(lat, lng);

                //Bewege Karte
                currentMarker.Position = (PointLatLng)pos;
                MainMap.Position = (PointLatLng)pos;
            }
        }

        /// <summary>
        /// Speichert einen neu eingegebenen Referenten in der DB. Zu diesem Zweck müssen Ortskoordinaten geholt werden
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSaveReferent_Click(object sender, EventArgs e)
        {
            
            string inputLastName = textLastName.Text;              
            string inputFirstName = textFirstName.Text; 
            string inputStreet = textStreet.Text;
            string inputPlace = textOrt.Text;
            string inputPLZ = textPLZ.Text;
            string inputKursLeiter = checkKursleiter.Checked ? "KL" : "BL";
            string inputAddress = "";

            string patternPLZ = "[1-9][0-9]{4}$";
            string patternWordChar = @"\w+";
            string patternNameChar = @"\w+\-?\w+$";
            string patternStreet = @"\w+";



            //Check for correct entry
            MatchCollection matchesLastName = Regex.Matches(inputLastName, patternWordChar);
            MatchCollection matchesFirstName = Regex.Matches(inputFirstName, patternWordChar);
            MatchCollection matchesPlace = Regex.Matches(inputPlace, patternWordChar);
            MatchCollection matchesStreet = Regex.Matches(inputStreet, patternStreet);
            MatchCollection matchesPLZ = Regex.Matches(inputPLZ, patternPLZ);
            string errorText = "";
            PointLatLng? pos=null;

            bool correctAddress = true;

            if (matchesLastName.Count == 0)
            {
                textLastName.Focus();
                correctAddress = false;
                errorText = "Name korrekt eingeben bitte!";
            }

            if (correctAddress == true)
            {
                if (matchesFirstName.Count == 0)
                {
                    textFirstName.Focus();
                    correctAddress = false;
                    errorText = "Vorname korrekt eingeben bitte!";
                }
            }

            if (correctAddress == true)
            {
                if (matchesStreet.Count == 0)
                {
                    textStreet.Focus();
                    correctAddress = false;
                    errorText = "Straße korrekt eingeben bitte!";
                }
            }

            if (correctAddress == true)
            {
                if (matchesPLZ.Count != 1)
                {
                    textPLZ.Focus();
                    correctAddress = false;
                    errorText = "PLZ korrekt eingeben bitte!";
                }
            }

            if (correctAddress == true)
            {
                if (matchesPlace.Count != 1)
                {
                    textOrt.Focus();
                    correctAddress = false;
                    errorText = "Ort korrekt eingeben bitte!";
                }
            }

            if (correctAddress == true)
            {
                //Write to db and update Table
                inputAddress = inputStreet + ", " + inputPLZ + ", " + inputPlace;

                //Code
                GeoCoderStatusCode status = GeoCoderStatusCode.Unknow;

                //Hole Geodaten für die Adresse:
                pos = GMapProviders.GoogleMap.GetPoint("Germany, " + inputAddress, out status);

                if (pos == null && status != GeoCoderStatusCode.G_GEO_SUCCESS)
                {
                    textOrt.Focus();
                    correctAddress = false;
                    errorText = "'Adresse bzw. Ort nicht bekannt!";
                }
            }


            if (correctAddress == true)
            {
                
                //Füge neue Reihe an DataGridView an:
                DataRow newRow = dtReferenten.NewRow();
                newRow["name"] = inputLastName;
                newRow["competence"] = inputKursLeiter;
                newRow["fname"] = inputFirstName;
                newRow["address"] = inputAddress;
                newRow["place"] = inputPlace;
                newRow["lat"] = pos.Value.Lat;
                newRow["lng"] = pos.Value.Lng;

                //Erneuere Tabelle
                dtReferenten.Rows.Add(newRow);
                dataGridViewReferenten.Update();

                //Füge ein in DB
                refCache.InsertNewReferent(newRow);
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBox.Show("Referent in Datenbank abgelegt: " + inputLastName, "Gespeichert", buttons);
                PointLatLng gPos = new PointLatLng();
                gPos.Lat = pos.Value.Lat;
                gPos.Lng = pos.Value.Lng;

                //Füge ein in Karte
                GMarkerGoogle refLocation = new GMarkerGoogle(gPos, GMarkerGoogleType.orange);
                refLocation.ToolTipMode = MarkerTooltipMode.OnMouseOver;
                refLocation.ToolTipText = inputLastName + ", " + inputFirstName + ", " +inputAddress;
                objects.Markers.Add(refLocation);




            }
            else
            {
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBox.Show(errorText, "Eingabefehler", buttons);
            }

        }
    }
}
