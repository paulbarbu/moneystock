using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Xml;
using System.Diagnostics; //todo remove
using System.Data.SqlServerCe;

namespace ms
{
    class DBHandler {
        private static string _fileName;
        private static string _password;
        private string _conStr;
        private SqlCeConnection _cn = null;

        public string fileName{
            get {
                return _fileName;
            }

            set {
                if(!hasExtension(value, ".sdf")){
                    value += ".sdf";
                }

                _fileName = value;
            }
        }

        public string password {
            set {
                _password = value;
            }
        }

        public DBHandler(string file_name, string pass) {
            fileName = file_name;
            password = pass;
            _conStr = string.Format("DataSource=\"{0}\"; Password='{1}'", fileName, _password);

            if (DBexists()) {
                connect();
            }
        }

        ~DBHandler() {
            disconnect();
        }

        public bool DBexists() {
            if (File.Exists(fileName)) {
                return true;
            }

            return false;
        }

        public void create() {
            SqlCeEngine en = new SqlCeEngine(_conStr);
            en.CreateDatabase();

            connect();
        }
        
        public bool isEmpty(string table_name) {
            string q = string.Format("SELECT * FROM {0}", table_name);

            SqlCeCommand cmd = new SqlCeCommand(q, _cn);
            SqlCeDataReader r;
            
            r = cmd.ExecuteReader();

            if (!r.Read()) {
                return true;
            }

            return false;
        }

        public bool existsTables(params string[] table_names) {
            string q = "SELECT table_name FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE <> 'VIEW'";
            ArrayList existing_tables = new ArrayList();

            SqlCeCommand cmd = new SqlCeCommand(q, _cn);
            SqlCeDataReader r;

            r = cmd.ExecuteReader();

            while(r.Read()) {
                existing_tables.Add(r.GetString(0));
            }

            foreach (string tablename in table_names) {
                if (!existing_tables.Contains(tablename)) {
                    return false;
                }
            }

            return true;
        }

        public void createTable(string table_query) {
            string q = "CREATE TABLE {0}";
            SqlCeCommand cmd = new SqlCeCommand(string.Format(q, table_query), _cn);
            cmd.ExecuteNonQuery();
        }

        public bool insert(string table_name, Dictionary<string, string> cols_vals, bool silent = false){
            string q = string.Format("INSERT INTO {0}({1}) VALUES('{2}')", table_name, string.Join(",", cols_vals.Keys.ToArray()), 
                string.Join("','", cols_vals.Values.ToArray()));
            int rows = 0;

            SqlCeCommand cmd = new SqlCeCommand(q, _cn);
            
            try {
                rows = cmd.ExecuteNonQuery();
            }
            catch (SqlCeException e) {
                if (!silent) {
                    throw e;
                }
            }

            if (rows > 0) {
                return true;
            }

            return false;
        }

        public SqlCeDataReader getData(string q) {
            SqlCeCommand cmd = new SqlCeCommand(q, _cn);

            return cmd.ExecuteReader();
        }

        public void delete(string query) {
            string q = string.Format("DELETE FROM {0}", query);

            SqlCeCommand cmd = new SqlCeCommand(q, _cn);
            cmd.ExecuteNonQuery();
        }

        private bool hasExtension(string str, string extension) {
            if (str.IndexOf(extension) > 0) {
                return true;
            }

            return false;
        }

        private void connect() {
            if (null == _cn) {
                _cn = new SqlCeConnection(_conStr);

                if (_cn.State == System.Data.ConnectionState.Closed) {
                    _cn.Open();
                }
            }
        }

        private void disconnect() {
            if (null != _cn) {
                _cn.Close();
            }
        }
    }
    
    class XMLDownloader : abstractDownloader {
        private string _URI;
        private WebClient _client;

        public string URI {
            get {
                return _URI;
            }

            set {
                _URI = value;
            }
        }

        public XMLDownloader(string URI) : base(URI){
            this.URI = URI;

            this._client = new WebClient();
        }

        public override string getData() {
            //TODO try-catch
            Stream ssource = this._client.OpenRead(this.URI);
            StreamReader reader = new StreamReader(ssource);
            string source = reader.ReadToEnd();

            ssource.Close();
            reader.Close();
            
            return source;
        }
    }

    class XmlParser : XmlTextReader {
        private string _element_name;
        private string _element_attr;
        private string _subelement_name;
        private string _subelement_attr;

        public XmlParser(string URL, string element_name, string element_attr, string subelement_name, string subelement_attr)
            : base(URL) {
            this._element_name = element_name;
            this._element_attr = element_attr;
            this._subelement_name = subelement_name;
            this._subelement_attr = subelement_attr;
        }

        public DateTime getDate() {
            DateTime dt = new DateTime();
            bool set = false;

            while (this.Read() && !set) {
                switch (this.NodeType) {
                    case XmlNodeType.Element:
                        if (this.Name == _element_name) {
                            while (this.MoveToNextAttribute()) {
                                if (this.Name == _element_attr) {
                                    dt = DateTime.Parse(this.Value);
                                    set = true;
                                    break;
                                }
                            }
                        }
                        break;
                }
            }


            return dt;
        }

        public Dictionary<string, Dictionary<string, decimal>> parse() {
            Dictionary<string, Dictionary<string, decimal>> currencies = new Dictionary<string, Dictionary<string, decimal>>();
            string current_key = null;
            string current_currency = null;

            while (this.Read()) {
                switch (this.NodeType) {
                    case XmlNodeType.Element:
                        if (this.Name == _element_name) {
                            while (this.MoveToNextAttribute()) {
                                if (this.Name == _element_attr) {
                                    currencies.Add(this.Value, new Dictionary<string, decimal>());
                                    current_key = this.Value;
                                }
                            }
                        }
                        else if (this.Name == _subelement_name && null != current_key) {
                            //TODO check if the key exists
                            while (this.MoveToNextAttribute()) {
                                if (this.Name == _subelement_attr) {
                                    current_currency = this.Value;
                                }
                            }
                        }
                        break;

                    case XmlNodeType.Text:
                        if (null != current_currency) {
                            currencies[current_key][current_currency] = decimal.Parse(this.Value);
                        }
                        break;
                }
            }

            /*foreach(var pair in currencies){
                Debug.WriteLine("{0} => (", pair.Key);
                foreach(var data in pair.Value ){
                    Debug.WriteLine("\t({0} => {1})\n", data.Key, data.Value);
                }
                Debug.WriteLine("),");
            }
            Debug.WriteLine(currencies);*/
            return currencies;
        }
    }

    class Converter {
        private Dictionary<string, decimal> _conversionRates;

        public readonly decimal ERR_NOFROM = -1;
        public readonly decimal ERR_NOTO = -2;

        public Dictionary<string, decimal> conversionRates{
            get {
                return _conversionRates;
            }

            set {
                _conversionRates = value;
            }
        }
        public Converter() {
        }

        public Converter(Dictionary<string, decimal> conversionRates) {
            _conversionRates = conversionRates;
        }

        public decimal convert(decimal amount, string from, string to) {
            from = from.ToUpper();
            to = to.ToUpper();

            if(!conversionRates.ContainsKey(from)){
                return ERR_NOFROM;
            }

            if(!conversionRates.ContainsKey(to)){
                return ERR_NOTO;
            }

            return (decimal) amount * conversionRates[from] / conversionRates[to];
        }
    }

    abstract class Item {
        private string _name;
        private string _symbol;
        private int _value;
        private XMLDownloader _dld; 

        public string Name {
            get {
                return _name;
            }

            set {
                _name = value;
            }
        }

        public string Symbol {
            get {
                return _symbol;
            }

            set {
                _symbol = value;
            }
        }

        public int Value {
            get {
                return _value;
            }

            set {
                _value = value;
            }
        }

        public XMLDownloader Dld {
            set {
                _dld = value;
            }
        }

        public Item(string name, string symbol, int value, XMLDownloader dld) {
            this.Name = name;
            this.Symbol = symbol;
            this.Value = value;
            this.Dld = dld;
        }

        public abstract int getValue(); // use this.Dld

        public abstract bool saveData();

        public abstract int plot();
    }

    abstract class abstractDownloader {
        private string _URI;

        protected abstractDownloader(string URI) {
            _URI = URI;
        }

        public abstract string getData();
    }
}
