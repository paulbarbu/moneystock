using System;
using System.Collections.Generic;
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
            get {
                return _password;
            }

            set {
                _password = value;
            }
        }

        public DBHandler(string file_name, string pass) {
            fileName = file_name;
            password = pass;
            _conStr = string.Format("DataSource=\"{0}\"; Password='{1}'", fileName, password);

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
            //TODO execute a select with no restrictions and check the number of results
            return true;
        }

        public bool existsTables(string table_name) {//TODO variable no of params
            string q = "SELECT table_name FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE <> 'VIEW'";

            SqlCeCommand cmd = new SqlCeCommand(q, _cn);
            SqlCeDataReader r;

            r = cmd.ExecuteReader();

            while (r.Read()) {
                Debug.WriteLine(r.GetString(0));
            }

            return true; //TODO if exists true, else false
        }

        public void createTables() { //variable number of params
            string q = "CREATE TABLE tesxt(LastName decimal, FirstName decimal, URL nvarchar (256) )";

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

    abstract class abstractDownloader {
        private string _URI;

        protected abstractDownloader(string URI){
            _URI = URI;
        }

        public abstract string getData();
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

    class currentXMLParser {
        private XmlTextReader _reader;
        private string _element_name;
        private string _subelement_name;
        private string _subelement_attr;
        
        public currentXMLParser(XmlTextReader reader, string element_name, string subelement_name, string subelement_attr) {
            this._reader = reader;
            this._element_name = element_name;
            this._subelement_name = subelement_name;
            this._subelement_attr = subelement_attr;
        }

        public Dictionary<string, decimal> parse() {
            Dictionary<string, decimal> currencies = new Dictionary<string, decimal>();
            string current_currency = null;

            while(_reader.Read()){
                switch(_reader.NodeType){
                    case XmlNodeType.Element:
                        if (_reader.Name == _subelement_name) {
                            //TODO check if the key exists
                            while(_reader.MoveToNextAttribute()){
                                if(_reader.Name == _subelement_attr){
                                    current_currency = _reader.Value;
                                }
                            }
                        }
                        break;

                    case XmlNodeType.Text:
                        if (null != current_currency) {
                            currencies.Add(current_currency, decimal.Parse(_reader.Value));
                        }
                        break;
                }
            }

            /*foreach(var data in currencies){
                Debug.WriteLine("\t({0} => {1})\n", data.Key, data.Value);
            }
            Debug.WriteLine("),");
            
            Debug.WriteLine(currencies);
             * */
            return currencies;
        }
    }

    class periodXMLParser {
        private XmlTextReader _reader;
        private string _element_name;
        private string _element_attr;
        private string _subelement_name;
        private string _subelement_attr;
        
        public periodXMLParser(XmlTextReader reader, string element_name, string element_attr,
            string subelement_name, string subelement_attr) {
            this._reader = reader;
            this._element_name = element_name;
            this._element_attr = element_attr;
            this._subelement_name = subelement_name;
            this._subelement_attr = subelement_attr;
        }

        public Dictionary<string, Dictionary<string, decimal>> parse() {
            Dictionary<string, Dictionary<string, decimal>> currencies = new Dictionary<string, Dictionary<string, decimal>>();
            string current_key = null;
            string current_currency = null;

            while(_reader.Read()){
                switch(_reader.NodeType){
                    case XmlNodeType.Element:
                        if (_reader.Name == _element_name) {
                            while(_reader.MoveToNextAttribute()){
                                if (_reader.Name == _element_attr) {
                                    currencies.Add(_reader.Value, new Dictionary<string, decimal>());
                                    current_key = _reader.Value;
                                }
                            }
                        }
                        else if (_reader.Name == _subelement_name && null != current_key) {
                            //TODO check if the key exists
                            while(_reader.MoveToNextAttribute()){
                                if(_reader.Name == _subelement_attr){
                                    current_currency = _reader.Value;
                                }
                            }
                        }
                        break;

                    case XmlNodeType.Text:
                        if (null != current_currency) {
                            currencies[current_key][current_currency] = decimal.Parse(_reader.Value);
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

    abstract class Converter{
        protected Dictionary<string, decimal> _conversionRates;

        protected Converter(Dictionary<string, decimal> conversionRates) {
            _conversionRates = conversionRates;
        }

        public abstract decimal convert(decimal amount, string from, string to);
    }

    class CurrencyConverter : Converter {
        public CurrencyConverter(Dictionary<string, decimal> conversionRates) : base(conversionRates) {
            this._conversionRates = conversionRates;
        }

        public override decimal convert(decimal amount, string from, string to) {
            //TODO implement this
            //TODO error codes for non-existing currencies
            Debug.WriteLine("currency converter convert");
            return 42.0M;
        }

        public void dbg() {
            foreach(var x in _conversionRates){
                Debug.WriteLine("{0} => {1}", x.Key, x.Value);
            }
        }

        public static void dbg(Dictionary<string, Dictionary<string, decimal>> d){
            foreach(var pair in d){
                    Debug.WriteLine("{0} => (", pair.Key);
                    foreach(var data in pair.Value ){
                        Debug.WriteLine("\t({0} => {1})\n", data.Key, data.Value);
                    }
                    Debug.WriteLine("),");
                }

                Debug.WriteLine(d);
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
}
