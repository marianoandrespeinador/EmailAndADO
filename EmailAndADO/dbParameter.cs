namespace EmailAndADO
{
    public struct dbParameter
    {
        private string name;
        private object dvalue;

        public dbParameter(string ParameterName, object ParameterValue)
        {
            name = ParameterName;
            dvalue = ParameterValue;
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }

        public object Value
        {
            get { return dvalue; }
            set { dvalue = value; }
        }
    }
}
