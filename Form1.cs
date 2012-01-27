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

namespace ms
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            bool populate = false;

            DBHandler db = new DBHandler("db", "moneystock"); //TODO create this in user/data

            //db.createTables();
            
            if (!db.DBexists()) {
                db.create();
                populate = true;
            }
            else if(!db.existsTables("foo")){ //TODO {{no table exists} -> create them or {if it's empty}} -> populate them
                populate = true;
            }

            int lastyear = DateTime.Now.AddYears(-1).Year;

            XmlTextReader yearReader = new XmlTextReader("http://www.bnro.ro/files/xml/years/nbrfxrates" + lastyear + ".xml");
            XmlTextReader last10Reader = new XmlTextReader("http://www.bnro.ro/nbrfxrates10days.xml");
            XmlTextReader cReader = new XmlTextReader("http://www.bnr.ro/nbrfxrates.xml");

            
            periodXMLParser yParser = new periodXMLParser(yearReader, "Cube", "date", "Rate", "currency");
            periodXMLParser last10Parser = new periodXMLParser(last10Reader, "Cube", "date", "Rate", "currency");
            currentXMLParser cParser = new currentXMLParser(cReader, "Cube", "Rate", "currency");

            Dictionary<string, Dictionary<string, decimal>> yRates = yParser.parse();
            Dictionary<string, Dictionary<string, decimal>> last10Rates = last10Parser.parse();
            Dictionary<string, decimal> cRates = cParser.parse();

            CurrencyConverter cc = new CurrencyConverter(cRates);

            cc.dbg();

            //TODO if populate, then populate the database and don;t freeze the form while doing it
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
