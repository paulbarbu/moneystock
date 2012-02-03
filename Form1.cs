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
        private DBHandler db = new DBHandler("db", "moneystock"); //TODO create the DB in user/data directorys
        private Converter c = new Converter();
        private Dictionary<string, string> aux, db_currencies, currencies = new Dictionary<string, string>() {
            {"EUR", "Euro"},
            {"AUD", "Dolar australian"},
            {"BGN", "Levă bulgărească"},
            {"CAD", "Dolar canadian"},
            {"CHF", "Franc elveţian"},
            {"CZK", "Coroană cehă"},
            {"DKK", "Coroană daneză"},
            {"EGP", "Liră egipteană"},
            {"GBP", "Liră sterlină"},
            {"HUF", "100 Forinţi maghiari"},
            {"JPY", "100 Yeni japonezi"},
            {"MDL", "Leu moldovenesc"},
            {"NOK", "Coroană norvegiană"},
            {"PLN", "Zlot polonez"},
            {"RUB", "Rublă rusească"},
            {"SEK", "Coroană suedeză"},
            {"TRY", "Liră turcească"},
            {"USD", "Dolar american"},
            {"ZAR", "Rand sud-african"},
            {"BRL", "Realul brazilian"},
            {"CNY", "Renminbi chinezesc"},
            {"INR", "Rupia indiană"},
            {"KRW", "100 Woni sud-coreeni"},
            {"MXN", "Peso mexican"},
            {"NZD", "Dolar neo-zeelandez"},
            {"RSD", "Dinar sârbesc"},
            {"UAH", "Hryvna ucraineană"},
            {"AED", "Dirhamul Emiratelor Arabe"},
            {"XAU", "Gram de aur"},
            {"XDR", "DST"},
        };

        private readonly int ERR_KEY_NOTFOUND = -1;
        private readonly string RON = "RON", RON_NAME = "Leu românesc";
        
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e) {
            label4.Hide();

            this.Hide();
            Form2 loading = new Form2();
            loading.Show();
            loading.Update();

            Dictionary<string, decimal> cRates =  initData();

            loading.Close();
            this.Visible = true;

            c.conversionRates = cRates;

            db_currencies = getDBCurrencies();
            aux = getDBCurrencies();
            
            int euro_pos = new int();
            int i = 0;

            comboBox1.Items.Add(RON_NAME);
            comboBox2.Items.Add(RON_NAME);

            foreach(var currency in db_currencies){
                comboBox1.Items.Add(currency.Value);
                comboBox2.Items.Add(currency.Value);

                if ("EUR" == currency.Key) {
                    euro_pos = i+1;
                }
                else {
                    i++;
                }                
            }

            db_currencies.Add(RON, RON_NAME);

            comboBox1.SelectedIndex = euro_pos;
            comboBox2.SelectedIndex = 0;

            textBox1.Text = "1";
        }

        private Dictionary<string, decimal> initData() {
            bool populate = false, no_fetch = false;
            DateTime? last_fetch = null;

            XmlParser currentXML = new XmlParser("http://www.bnr.ro/nbrfxrates.xml", "Cube", "date", "Rate", "currency");
            XmlParser dateXML = new XmlParser("http://www.bnr.ro/nbrfxrates.xml", "Cube", "date", "Rate", "currency");

            Dictionary<string, decimal> cRates = currentXML.parse().First().Value;

            DateTime cDate = dateXML.getDate();
            
            if (!db.DBexists()) {
                db.create();
                db.createTable("data(last_fetch DATETIME UNIQUE NOT NULL, oldest_date DATETIME)");
                db.createTable("currency(symbol NCHAR(3) NOT NULL, name NVARCHAR(35) NOT NULL)");

                populate = no_fetch = true;

                foreach (string table in cRates.Keys) {
                    db.createTable(table + "(rate MONEY NOT NULL, date DATETIME UNIQUE NOT NULL)");

                    if (currencies.ContainsKey(table)) {
                        db.insert("currency", new Dictionary<string, string>() {{"symbol", table}, {"name", currencies[table]}});
                    }
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
                    Dictionary<string, string> d = new Dictionary<string, string>();
                    d.Add("rate", cRates[table].ToString());
                    d.Add("date", cDate.ToString());

                    if (!db.insert(table, d, true)) {
                        //TODO raise error
                    }
                }

                if (null != last_fetch) {
                    db.delete(string.Format("data WHERE last_fetch='{0}'", last_fetch));
                }

                Dictionary<string, string> data = new Dictionary<string, string>();
                data.Add("last_fetch", cDate.ToString());
                db.insert("data", data, true);

                last_fetch = cDate;
            }

            TimeSpan ts = (TimeSpan)(cDate - last_fetch);

            if (populate || ts.Days >= 10) {
                XmlParser last10XML = new XmlParser("http://www.bnro.ro/nbrfxrates10days.xml", "Cube", "date", "Rate", "currency");
                Dictionary<string, Dictionary<string, decimal>> last10Rates = last10XML.parse();

                Dictionary<string, string> d = new Dictionary<string, string>();

                foreach (var date in last10Rates) {
                    d["date"] = date.Key;

                    if (DateTime.Parse(date.Key) != last_fetch) {
                        foreach (var currency in date.Value) {
                            d["rate"] = currency.Value.ToString();
                            db.insert(currency.Key, d, true);
                        }
                    }
                }
            }


            if (populate) {
                int lastyear = DateTime.Now.AddYears(-1).Year;

                XmlParser yearXML = new XmlParser("http://www.bnro.ro/files/xml/years/nbrfxrates" + lastyear + ".xml", "Cube", "date", "Rate", "currency");
                Dictionary<string, Dictionary<string, decimal>> yRates = yearXML.parse();
                Dictionary<string, string> d = new Dictionary<string, string>();

                foreach (var date in yRates) {
                    d["date"] = date.Key;

                    if (DateTime.Parse(date.Key) != last_fetch) {
                        foreach (var currency in date.Value) {
                            d["rate"] = currency.Value.ToString();
                            db.insert(currency.Key, d, true);
                        }
                    }
                }
            }

            cRates.Add(RON, 1);

            return cRates;
        }

        private void updateUI(string amount, string from, string to) {
            decimal amount_from;
            
            amount = delInvalidChars(amount, "0123456789,.".ToCharArray());

            textBox1.Text = amount;

            if ("" == amount) {
                label4.Hide();
                return;
            }
            else {
                label4.Show();
            }

            bool success = decimal.TryParse(amount, out amount_from);
            
            if (!success) {
                MessageBox.Show("Sunt permise doar cifrele!", "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                amount_from = 0;
                textBox1.Text = "";
                label4.Hide();
            }

            decimal value = c.convert(amount_from, from, to);

            if(value == c.ERR_NOFROM || value == c.ERR_NOTO){
                MessageBox.Show("Această monedă nu există!", "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            string text = string.Format("{0} {1} = {2} {3}", amount_from, from, Decimal.Round(value, 3, MidpointRounding.AwayFromZero), to);

            label4.Text = text;
        }

        private void textBox1_TextChanged(object sender, EventArgs e) {
            string from = getKeyByValue(db_currencies, (string) comboBox1.SelectedItem);
            string to = getKeyByValue(db_currencies, (string) comboBox2.SelectedItem);
            updateUI(textBox1.Text, from, to);
        }

        public string delInvalidChars(string original, char[] valid) {
            StringBuilder outStr = new StringBuilder();

            foreach (char c in original) {
                if (valid.Contains(c)) {
                    outStr.Append(c);
                }
            }

            return outStr.ToString();
        }

        private Dictionary<string, string> getDBCurrencies() {
            Dictionary<string, string> d = new Dictionary<string, string>();
            SqlCeDataReader r;

            r = db.getData("SELECT * FROM currency");

            while (r.Read()) {
                d.Add(r.GetString(0), r.GetString(1));
            }

            return d;
        }

        private string getKeyByValue(Dictionary<string, string> d, string value) {
            foreach (var entry in d) {
                if(entry.Value == value){
                    return entry.Key;
                }
            }

            return ERR_KEY_NOTFOUND.ToString();
        }

        private bool isValidDate(DateTime dt) {
            if (dt <= DateTime.Today) {
                SqlCeDataReader r;

                r = db.getData(string.Format("SELECT TOP(1) * FROM eur WHERE date <= '{0}' ORDER BY date DESC", dt));

                if (r.Read()) {
                    return true;
                }
            }

            return false;
        }

        private Dictionary<string, decimal> getDBRates(DateTime dt) {
            Dictionary<string, decimal> d = new Dictionary<string, decimal>();
            SqlCeDataReader r;

            r = db.getData(string.Format("SELECT * FROM eur WHERE date = '{0}'", dt));

            if (r.Read()) {
                foreach (var c in aux) {
                    r = db.getData(string.Format("SELECT rate FROM {0} WHERE date = '{1}'", c.Key, dt));
                    r.Read();
                    d.Add(c.Key, r.GetDecimal(0));
                }

                d.Add(RON, 1);
            }
            else {
                foreach (var c in aux) {
                    r = db.getData(string.Format("SELECT TOP(1) rate FROM {0} WHERE date < '{1}' ORDER BY date DESC", c.Key, dt));
                    r.Read();
                    d.Add(c.Key, r.GetDecimal(0));
                }

                d.Add(RON, 1);
            }

            return d;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) {
            string from = getKeyByValue(db_currencies, (string) comboBox1.SelectedItem);
            string to = getKeyByValue(db_currencies, (string) comboBox2.SelectedItem);
            updateUI(textBox1.Text, from, to);
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e) {
            string from = getKeyByValue(db_currencies, (string)comboBox1.SelectedItem);
            string to = getKeyByValue(db_currencies, (string)comboBox2.SelectedItem);
            updateUI(textBox1.Text, from, to);
        }

        private void dateTimePicker1_ValueChanged(object sender, EventArgs e) {
            if (isValidDate(dateTimePicker1.Value.Date)) {
                c.conversionRates = getDBRates(dateTimePicker1.Value.Date);
            }
            else {
                MessageBox.Show("Nu există rate de schimb pentru această dată!", "Eroare", MessageBoxButtons.OK, MessageBoxIcon.Error);
                dateTimePicker1.Value = DateTime.Today;
            }

            string from = getKeyByValue(db_currencies, (string)comboBox1.SelectedItem);
            string to = getKeyByValue(db_currencies, (string)comboBox2.SelectedItem);
            updateUI(textBox1.Text, from, to);
        }
    }
}
