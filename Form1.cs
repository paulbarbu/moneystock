using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using System.Diagnostics; //todo remove
using System.Xml;
using System.Data.SqlServerCe;

namespace ms
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            bool populate = false, no_fetch = false;
            DateTime? last_fetch = null;

            DBHandler db = new DBHandler("db", "moneystock"); //TODO create the DB in user/data directorys

            XmlTextReader cReader = new XmlTextReader("http://www.bnr.ro/nbrfxrates.xml");
            XmlTextReader dateReader = new XmlTextReader("http://www.bnr.ro/nbrfxrates.xml");
            currentXMLParser cParser = new currentXMLParser(cReader, "Cube", "Rate", "currency", "date");

            Dictionary<string, decimal> cRates = cParser.parse();

            cParser.Reader = dateReader;
            DateTime cDate = cParser.getDate();

            //CurrencyConverter cc = new CurrencyConverter(cRates);
            //cc.dbg();
            
            if (!db.DBexists()) {
                db.create();
                db.createTable("data(last_fetch DATETIME UNIQUE NOT NULL)");

                populate = no_fetch = true;

                foreach (string table in cRates.Keys) {
                    db.createTable(table + "(rate MONEY NOT NULL, date DATETIME UNIQUE NOT NULL)");
                }
            }
            else {
                SqlCeDataReader r = db.getData("SELECT last_fetch FROM data");

                if (r.Read()) {
                    last_fetch = r.GetDateTime(0);

                    if (last_fetch != cDate) {
                        no_fetch = true;
                    }
                }
                else {
                    no_fetch = true;
                }
            }

            if (no_fetch) {
                foreach (string table in cRates.Keys) {
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    d.Add("rate", cRates[table]);
                    d.Add("date", cDate);

                    if (!db.insert(table, d, true)) {
                        //TODO raise error
                    }
                }
                
                if (null != last_fetch) {
                    db.delete(string.Format("data WHERE last_fetch='{0}'", last_fetch));
                }

                Dictionary<string, object> data = new Dictionary<string,object>();
                data.Add("last_fetch", cDate);
                db.insert("data", data, true);

                last_fetch = cDate;
            }

            if (null != last_fetch) {
                TimeSpan ts = (TimeSpan)(cDate - last_fetch);

                if (populate || ts.Days >= 10) {
                    XmlTextReader last10Reader = new XmlTextReader("http://www.bnro.ro/nbrfxrates10days.xml");
                    periodXMLParser last10Parser = new periodXMLParser(last10Reader, "Cube", "date", "Rate", "currency");
                    Dictionary<string, Dictionary<string, decimal>> last10Rates = last10Parser.parse();

                    Dictionary<string, object> d = new Dictionary<string,object>();

                    foreach(var date in last10Rates){
                        d["date"] = date.Key;

                        if (DateTime.Parse(date.Key) != last_fetch) {
                            foreach (var currency in date.Value) {
                                d["rate"] = currency.Value;
                                db.insert(currency.Key, d, true);
                            }
                        }
                    }
                }
            }
       
            
            if (populate) {
                int lastyear = DateTime.Now.AddYears(-1).Year;

                XmlTextReader yearReader = new XmlTextReader("http://www.bnro.ro/files/xml/years/nbrfxrates" + lastyear + ".xml");
                periodXMLParser yParser = new periodXMLParser(yearReader, "Cube", "date", "Rate", "currency");
                Dictionary<string, Dictionary<string, decimal>> yRates = yParser.parse();
                //TODO if populate, then populate the database and don;t freeze the form while doing it

                Dictionary<string, object> d = new Dictionary<string, object>();

                foreach (var date in yRates) {
                    d["date"] = date.Key;

                    if (DateTime.Parse(date.Key) != last_fetch) {
                        foreach (var currency in date.Value) {
                            d["rate"] = currency.Value;
                            db.insert(currency.Key, d, true);
                        }
                    }
                }
            }
        }

        private void scrapeData(object sender, EventArgs e)
        {
            WebClient client = new WebClient();

            Stream ssource = client.OpenRead("http://www.bvb.ro/ListedCompanies/SecurityDetail.aspx?s=FP&t=1");
            StreamReader reader = new StreamReader(ssource);
            string source = reader.ReadToEnd();

            int mark_position = source.IndexOf("<span id=\"ctl00_central_lbVar\"");
            int start = mark_position + source.Substring(mark_position).IndexOf('>') + 1;
            int length = source.Substring(start).IndexOf('<');
          
            label1.Text = source.Substring(start, length);

            ssource.Close();
            reader.Close();
        }
    }
}
