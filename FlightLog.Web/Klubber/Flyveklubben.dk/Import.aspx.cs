﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using global::System.Data;
using FlightLog.Models;

namespace FlightLog.Klubber.FlyveklubbenDk
{
    using global::System.Net;

    public partial class Import : global::System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            HttpContext.Current.Response.Write(this.ImportFlightLog(this.DownloadDataFile()));
        }

        private string DownloadDataFile()
        {
            var target = Server.MapPath("~/App_Data/00096B5FAB4D.xml");
            if (global::System.IO.File.Exists(target))
            {
                var creationTime = (global::System.IO.File.GetCreationTime(target));
                if (DateTime.Now.Date != creationTime.Date)
                {
                    global::System.IO.File.Delete(target);
                }
                else
                {
                    HttpContext.Current.Response.Write("Data file current.<br />");
                    return target;   
                }
            }

            try
            {
                HttpContext.Current.Response.Write("Downloading Current Data file.<br />");
                WebClient client = new WebClient();
                client.DownloadFile("http://flyveklubben.dk/Application/StartStedet/00096B5FAB4D.xml", target);
                HttpContext.Current.Response.Write("Data file downloaded.<br />");
                return target;
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        /// <summary>
        /// Import flight information from deprecated xml source 
        /// </summary>
        /// <returns>Status report on the import job</returns>
        /// <remarks>Will be removed to separate Assembly prior to beta release</remarks>
        public string ImportFlightLog(string startStedetPath)
        {
            //string startStedetPath = @"\Flyveklubben\StartStedetSync\Webservice\Application\StartStedet\00096B5FAB4D.xml";

            //if (global::System.IO.Directory.Exists(@"C:\Users\Jhe\Documents\Visual Studio 2010\Projects"))
            //{
            //    startStedetPath = @"C:\Users\Jhe\Documents\Visual Studio 2010\Projects" + startStedetPath;
            //}
            //else if (global::System.IO.Directory.Exists(@"C:\Users\Jan\Documents\Visual Studio 2010\Projects"))
            //{
            //    startStedetPath = @"C:\Users\Jan\Documents\Visual Studio 2010\Projects" + startStedetPath;
            //}
            //else if (global::System.IO.Directory.Exists(@"C:\Users\Jan\Workshop\Visual Studio 2010\Projects"))
            //{
            //    startStedetPath = @"C:\Users\Jan\Workshop\Visual Studio 2010\Projects" + startStedetPath;
            //}
            //else
            //{
            //    return "Visual Studio 2010 Projects could not be found.";
            //}

            var startListeDataTable = new StartListeData.StartListeDataSet.StartListeDataTable();
            if (global::System.IO.File.Exists(startStedetPath))
            {
                startListeDataTable.ReadXml(startStedetPath);
            }
            else
            {
                return startStedetPath + " <br/>Source Import file not found.";
            }

            int i = 0;
            int omitted = 0;
            int invalid = 0;
            using (var db = new FlightContext())
            {
                var club = db.Clubs.First(c => c.ShortName == "ØSF");
                var spilstart = db.StartTypes.First(c => c.Name == "Spilstart");
                foreach (DataRow row in startListeDataTable.Rows)
                {
                    if (i > 50)
                    {
                        break;
                    }

                    int rkey = 0;

                    // Custom data is not imported (could be old manual imported data)
                    if (!Int32.TryParse(row["RecordKey"].ToString(), out rkey))
                    {
                        invalid++;
                        continue;
                    }

                    // Allready imported
                    if (db.Flights.SingleOrDefault(f => f.RecordKey == rkey) != null)
                    {
                        omitted++;
                        continue;
                    }

                    // Starting location 
                    var startedFrom = row["StartedFrom"].ToString().Trim();
                    Location departure = db.Locations.SingleOrDefault(l => l.Name.ToUpper() == startedFrom.ToUpper());
                    if (departure == null)
                    {
                        departure = new Location() { Name = startedFrom };
                        db.Locations.Add(departure);
                        db.SaveChanges();
                    }

                    // Landing location
                    var landedOn = row["landedOn"].ToString().Trim();
                    Location landing = db.Locations.SingleOrDefault(l => l.Name.ToUpper() == landedOn.ToUpper());
                    if ((!string.IsNullOrEmpty(landedOn)) && (landing == null))
                    {
                        landing = new Location() { Name = landedOn };
                        db.Locations.Add(landing);
                        db.SaveChanges();
                    }

                    // Plane
                    var planeName = row["PlaneName"].ToString().Trim();
                    Plane plane = db.Planes.SingleOrDefault(l => l.CompetitionId.ToUpper() == planeName.ToUpper());
                    if (plane == null)
                    {
                        double seats = 1;
                        if (((!string.IsNullOrEmpty(row["Pilot2_MemberId"].ToString())) &&
                             (row["Pilot2_MemberId"].ToString() != "0")) || (planeName == "R2") || (planeName == "RR"))
                        {
                            seats = 2;
                        }
                        plane = new Plane()
                        {
                            CompetitionId = planeName,
                            Registration = row["PlaneRegistration"].ToString().Trim(),
                            Seats = seats,
                            DefaultStartType = spilstart,
                            Engines = 0,
                            EntryDate = DateTime.Now
                        };
                        db.Planes.Add(plane);
                        db.SaveChanges();
                    }

                    // Pilot1
                    var pilot1 = row["Pilot1_MemberId"].ToString().Trim();
                    Pilot pilot = db.Pilots.SingleOrDefault(l => l.MemberId.ToUpper() == pilot1.ToUpper());
                    if (pilot == null)
                    {
                        pilot = new Pilot()
                        {
                            MemberId = pilot1,
                            UnionId = row["Pilot1_UnionId"].ToString().Trim(),
                            Name = row["Pilot1_Firstname"].ToString().Trim() + " " + row["Pilot1_Lastname"].ToString().Trim(),
                            Club = club
                        };
                        db.Pilots.Add(pilot);
                        db.SaveChanges();
                    }

                    // Pilot2
                    var pilot2 = row["Pilot2_MemberId"].ToString().Trim();
                    Pilot pilotBackseat = db.Pilots.SingleOrDefault(l => l.MemberId.ToUpper() == pilot2.ToUpper());
                    if ((!string.IsNullOrEmpty(pilot2)) && (pilot2 != "0") && (pilotBackseat == null))
                    {
                        pilotBackseat = new Pilot()
                        {
                            MemberId = pilot2,
                            UnionId = row["Pilot2_UnionId"].ToString().Trim(),
                            Name = row["Pilot2_Firstname"].ToString().Trim() + " " + row["Pilot2_Lastname"].ToString().Trim(),
                            Club = club
                        };
                        db.Pilots.Add(pilotBackseat);
                        db.SaveChanges();
                    }

                    // Payment
                    var betaler = row["Betaler_MemberId"].ToString().Trim();
                    Pilot pilotBetaler = db.Pilots.SingleOrDefault(l => l.MemberId.ToUpper() == betaler.ToUpper());
                    if (pilotBetaler == null)
                    {
                        pilotBetaler = new Pilot()
                        {
                            MemberId = betaler,
                            UnionId = row["Betaler_UnionId"].ToString().Trim(),
                            Name = row["Betaler_Firstname"].ToString().Trim() + " " + row["Betaler_Lastname"].ToString().Trim(),
                            Club = club
                        };
                        db.Pilots.Add(pilotBetaler);
                        db.SaveChanges();
                    }

                    // StartType
                    var startdesc = row["StartTypeDescription"].ToString().Trim();
                    var start = db.StartTypes.SingleOrDefault(s => s.Name == startdesc);
                    if (start == null)
                    {
                        start = new StartType()
                        {
                            Name = startdesc,
                            Club = club
                        };
                        db.StartTypes.Add(start);
                        db.SaveChanges();
                    }

                    // updater
                    var insertedBy = row["InsertedBy_Firstname"].ToString().Trim() + " " +
                                     row["InsertedBy_Lastname"].ToString().Trim();
                    Pilot updater = db.Pilots.Where(l => l.Name.ToUpper() == insertedBy.ToUpper()).FirstOrDefault();
                    if (updater == null)
                    {
                        // Need memberId for creating member
                    }

                    var flight = new Flight
                    {
                        Date = DateTime.Parse(row["SDate"].ToString()),
                        Departure = DateTime.Parse(row["STime"].ToString()),
                        Landing = DateTime.Parse(row["LTime"].ToString()),
                        PlaneId = plane.PlaneId,
                        PilotId = pilot.PilotId,
                        BetalerId = pilotBetaler.PilotId,
                        StartedFromId = departure.LocationId
                    };

                    if (pilotBackseat != null)
                    {
                        flight.PilotBackseatId = pilotBackseat.PilotId;
                    }

                    if (landing != null)
                    {
                        flight.LandedOnId = landing.LocationId;
                    }

                    flight.LastUpdated = DateTime.Now;
                    if (updater != null)
                    {
                        flight.LastUpdatedBy = updater.ToString();
                    }

                    flight.StartType = start;

                    flight.StartCost = Double.Parse(row["StartCost"].ToString());
                    flight.FlightCost = Double.Parse(row["FlightCost"].ToString());
                    flight.TachoCost = Double.Parse(row["TachoCost"].ToString());

                    flight.TaskDistance = Int32.Parse(row["Km"].ToString());
                    flight.RecordKey = Int32.Parse(row["RecordKey"].ToString());

                    db.Flights.Add(flight);

                    db.SaveChanges();
                    i++;
                }
            }

            return
                string.Format(
                    "Imported {0} new flights.<br />Parsed {3} of {6}(allready imported {1}, skipped {2})<br /> <a href=\"{4}\">{5}</a><br /><br />Continuing import in 10 seconds...<script>setTimeout('window.location.reload()',10000);</script>",
                    i,
                    omitted,
                    invalid,
                    i + omitted + invalid,
                    "/Flight",
                    "Back to list",
                    startListeDataTable.Rows.Count);

            ////return RedirectToAction("Index");
        }
    }
}