using System;
using System.Collections.Generic;

namespace at.spi.Tools
{
    class CmdArgs
    {
        IDictionary<string, string> opt;

        public CmdArgs(string[] args)
        {
            opt = new Dictionary<string, string>();

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (!(a.StartsWith("-") || a.StartsWith("/")))
                {
                    continue;
                }
                a = a.Remove(0, 1);

                string k, v = null;
                if (a.Contains("="))
                {
                    string[] temp = a.Split(new char[] { '=' });
                    k = temp[0];
                    v = temp[1];
                }
                else
                {
                    k = a;
                    if ( i+1 < args.Length )
                    {
                        // is the next token a value or an option
                        v = args[i+1];
                        if ( IsSwitch(v) )
                        {
                            v = null;
                        }
                    }
                }

                opt.Add(k.ToLower(), v);
            }
        }
        private bool IsSwitch(string OptToTest)
        {
            return OptToTest.StartsWith("-") || OptToTest.StartsWith("/");
        }
        public bool GetInt(string key, out int value, int DefaultValue)
        {
            string strVal;
            if (!GetString(key, out strVal, null))
            {
                value = DefaultValue;
                return false;
            }

            return int.TryParse(strVal, out value);
        }

        public bool GetUInt(string key, out uint value, uint DefaultValue)
        {
            string strVal;
            if (!GetString(key, out strVal, null))
            {
                value = DefaultValue;
                return false;
            }

            return uint.TryParse(strVal, out value);
        }

        public string GetString_ThrowIfNotExists(string key)
        {
            string result;
            if ( GetString(key, out result, null) )
            {
                return result;
            }

            throw new Exception( String.Format("Parameter [{0}] is not given", key) );
        }
        public string GetString(string key)
        {
            string result;
            GetString(key, out result, null);
            return result;
        }
        public bool GetString(string key, out string value)
        {
            return GetString(key, out value, null);
        }
        public bool GetString(string key, out string value, string DefaultValue)
        {
            if (!opt.TryGetValue(key, out value))
            {
                value = DefaultValue;
                return false;
            }
            return true;
        }
        public bool exists(string key)
        {
            if ( key == null)
                return false;

            return opt.ContainsKey(key.ToLower());
        }
        public bool CheckMustParams(string[] MustParams, out string ParamsMissing)
        {
            ParamsMissing = "";
            foreach (string s in MustParams)
            {
                if (!this.exists(s))
                {
                    ParamsMissing += s + ",";
                }
            }
            return String.IsNullOrEmpty(ParamsMissing);
        }
    }
}
