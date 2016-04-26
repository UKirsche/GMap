using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;

namespace Demo.WindowsForms
{
#if SQLite
    using System.Collections.Generic;
    using System.Data.Common;
#if !MONO
    using System.Data.SQLite;
#else
   using SQLiteConnection=Mono.Data.Sqlite.SqliteConnection;
   using SQLiteTransaction=Mono.Data.Sqlite.SqliteTransaction;
   using SQLiteCommand=Mono.Data.Sqlite.SqliteCommand;
   using SQLiteDataReader=Mono.Data.Sqlite.SqliteDataReader;
   using SQLiteParameter=Mono.Data.Sqlite.SqliteParameter;
#endif
    using System.IO;
    using System.Text;
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Data;
    using System.Data.SqlClient;



    /// <summary>
    /// Cache for Referents
    /// </summary>
    internal class SQLiteReferentenCache
    {
        string cache;
        string refCache;
        string db;



        /// <summary>
        /// Get all referents  as GMapMarker
        /// </summary>
        public List<GMapMarker> ReturnReferentsAsGMapMarker()
        {
            //Create List
            List<GMapMarker> listMarkerReferents = new List<GMapMarker>();

            //Lese RefCache aus
            DataTable retTable = GetAllDataFromCache();
            foreach (DataRow dr in retTable.Rows)
            {
                GMapMarker refLocation;

                string competenceKL = dr["competence"].ToString();
                double locLat = Convert.ToDouble(dr["lat"]);
                double locLng = Convert.ToDouble(dr["lng"]);
                PointLatLng lPos = new PointLatLng();
                lPos.lat = locLat;
                lPos.lng = locLng;
                string toolTip = dr["name"] + ", " + dr["fname"] + "\n" + dr["address"];

                if (competenceKL.Equals("KL"))
                {
                    refLocation = new GMarkerGoogle(lPos, GMarkerGoogleType.orange);
                }
                else
                {
                    refLocation = new GMarkerGoogle(lPos, GMarkerGoogleType.red_small);
                }
                
                refLocation.ToolTipMode = MarkerTooltipMode.OnMouseOver;
                refLocation.ToolTipText = toolTip;

                listMarkerReferents.Add(refLocation);
            }

            return listMarkerReferents;
        }


        /// <summary>
        /// Fetches each Referent and adds geo location to referent
        /// </summary>
        public void AddGeoInfoToReferents()
        {
            GeoCoderStatusCode status = GeoCoderStatusCode.Unknow;

            //Falls Geo-Koos nicht vorhanden, bitte nachtragen 
            if (!CheckGeoExists())
            {
                DataTable retTable = GetAllDataFromCache();
                
                //Ermittle die geo-koos für die orte
                foreach (DataRow dr in retTable.Rows)
                {   //Extrahiere relevante werte für referenten
                    string ort = dr["place"].ToString();
                    string adress = dr["address"].ToString();
                    PointLatLng pos;

                    try
                    {
                        pos = (PointLatLng)GMapProviders.GoogleMap.GetPoint(adress, out status);
                    }
                    catch(Exception ex){
                        pos = (PointLatLng)GMapProviders.GoogleMap.GetPoint(ort, out status);
                    } 
               
                               
                    if (pos != null && status == GeoCoderStatusCode.G_GEO_SUCCESS)
                    {
                        //Update DB
                        AppendGeoDataRow(pos, adress);
                      
                    }

                }
            }          

        }

        public string RefCach
        {
            get
            {
                return refCache;
            }
        }

        #region CacheLocation ReferentenDB
        /// <summary>
        /// local cache location for ReferentenDB
        /// </summary>
        public string CacheLocation
        {
            get
            {
                return cache;
            }
            set
            {
                cache = value;
                refCache = Path.Combine(cache, "ReferentenCacheDB") + Path.DirectorySeparatorChar;
                db = refCache + "RefData.sqlite";
            }
        }
        #endregion

        #region Füge Geodaten zu Datensätzen hinzu
        /// <summary>
        /// Append GeoData to datasets
        /// </summary>
        /// <param name="ip"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool AppendGeoDataRow(PointLatLng pos, string id)
        {
            bool ret = true;
            try
            {
                using (SQLiteConnection cn = new SQLiteConnection())
                {
#if !MONO
                    cn.ConnectionString = string.Format("Data Source=\"{0}\";", db);
#else
               cn.ConnectionString = string.Format("Version=3,URI=file://{0},FailIfMissing=True,Default Timeout=33", db);
#endif
                    cn.Open();
                    using (SQLiteCommand command = new SQLiteCommand(cn))
                    {
                        command.CommandText = @"Update RefCache Set lat = @lat, lng = @lng where address LIKE @address";
                        command.Parameters.Add(new SQLiteParameter("@lat", pos.lat));
                        command.Parameters.Add(new SQLiteParameter("@lng", pos.lng));
                        command.Parameters.Add(new SQLiteParameter("@address", id));
                        command.ExecuteNonQuery();
                    }
                    cn.Close();
                }
            }
            catch (Exception ex)
            {
#if MONO
            Console.WriteLine("PutDataToCache: " + ex.ToString());
#endif
                Debug.WriteLine("PutDataToCache: " + ex.ToString());
                ret = false;
            }
            return ret;
        }
        #endregion

        #region Check Geo-Data vorhanden
        /// <summary>
        /// Check whether Referent-Data already enriched with Geo-Location
        /// </summary>
        /// <returns></returns>
        private bool CheckGeoExists()
        {
            bool ret = false;
            try
            {
                using (SQLiteConnection cn = new SQLiteConnection())
                {
#if !MONO
                    cn.ConnectionString = string.Format("Data Source=\"{0}\";", db);
#else
                    cn.ConnectionString = string.Format("Version=3,URI=file://{0},Default Timeout=33", db);
#endif
                    cn.Open();
                    {
                        using (DbCommand com = cn.CreateCommand())
                        {
                            com.CommandText = "SELECT lat FROM RefCache LIMIT 1";

                            using (DbDataReader rd = com.ExecuteReader())
                            {
                                if (rd.Read())
                                {
                                    double number;
                                    if (Double.TryParse(rd["lat"].ToString(), out number)){
                                        ret = true;
                                    }
                                }
                                rd.Close();
                            }
                        }
                    }
                    cn.Close();
                }
            }
            catch (Exception ex)
            {
#if MONO
            Console.WriteLine("GetDataFromCache: " + ex.ToString());
#endif
                Debug.WriteLine("GetDataFromCache: " + ex.ToString());
                ret = false;
            }
            return ret;
        }


        #region Lese alle Referenten-Cachedaten aus SQLite
        /// <summary>
        /// Get all referent data form cache
        /// </summary>
        /// <returns>DataTable</returns>
        public void InsertNewReferent(DataRow newDataRow)
        {
            //string firstName = newDataRow;
            string name = newDataRow.ItemArray[0].ToString();
            string fname = newDataRow.ItemArray[1].ToString();
            string place = newDataRow.ItemArray[2].ToString();
            string region = newDataRow.ItemArray[3].ToString();
            string address = newDataRow.ItemArray[4].ToString();
            string competence = newDataRow.ItemArray[5].ToString();
            double lat = Convert.ToDouble(newDataRow.ItemArray[6]);
            double lng = Convert.ToDouble(newDataRow.ItemArray[7]);

            try
            {
                using (SQLiteConnection cn = new SQLiteConnection())
                {
                    cn.ConnectionString = string.Format("Data Source=\"{0}\";", db);

                    SQLiteCommand cmd;

                    cn.Open();  //Initiate connection to the db
                    cmd = cn.CreateCommand();
                    cmd.CommandText = "INSERT INTO RefCache (name, fname, place, region, address, competence, lat, lng) VALUES (@name, @fname, @place, @region, @address, @competence, @lat, @lng)";
                    cmd.Parameters.AddWithValue("@name", name);
                    cmd.Parameters.AddWithValue("@fname", fname);
                    cmd.Parameters.AddWithValue("@place", place);
                    cmd.Parameters.AddWithValue("@region", region);
                    cmd.Parameters.AddWithValue("@address", address);
                    cmd.Parameters.AddWithValue("@competence", competence);
                    cmd.Parameters.AddWithValue("@lat", lat);
                    cmd.Parameters.AddWithValue("@lng", lng);
                    cmd.ExecuteNonQuery();
                    cn.Close();

                }
            }
            catch (Exception ex)
            {
#if MONO
            Console.WriteLine("GetDataFromCache: " + ex.ToString());
#endif
                Debug.WriteLine("Insert Error: " + ex.ToString());
            }

        }
        #endregion


        #region Lese alle Referenten-Cachedaten aus SQLite
        /// <summary>
        /// Get all referent data form cache
        /// </summary>
        /// <returns>DataTable</returns>
        public DataTable GetAllDataFromCache()
        {
            DataTable ret = new DataTable();   
            try
            {
                using (SQLiteConnection cn = new SQLiteConnection())
                {
                    cn.ConnectionString = string.Format("Data Source=\"{0}\";", db);

                    SQLiteDataAdapter ad;
                    SQLiteCommand cmd;

                    cn.Open();  //Initiate connection to the db
                    cmd = cn.CreateCommand();
                    cmd.CommandText = "Select * FROM RefCache Order By name";  //set the passed query
                    ad = new SQLiteDataAdapter(cmd);
                    ad.Fill(ret); //fill the datasource
                    cn.Close();
              
                }
            }
            catch (Exception ex)
            {
#if MONO
            Console.WriteLine("GetDataFromCache: " + ex.ToString());
#endif
                Debug.WriteLine("GetDataFromCache: " + ex.ToString());
            }

            return ret;
        }
        #endregion
    }
    #endregion
}
#endif