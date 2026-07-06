using System.Dynamic;
using System.Linq;

namespace AxarDB.Wrappers
{
    public class CaseInsensitiveDocumentWrapper : DocumentWrapper
    {
        public CaseInsensitiveDocumentWrapper(Dictionary<string, object> document) : base(document)
        {
        }

        public override bool TryGetMember(GetMemberBinder binder, out object? result)
        {
            var key = Data.Keys.FirstOrDefault(k => string.Equals(k, binder.Name, System.StringComparison.OrdinalIgnoreCase));
            if (key != null && Data.TryGetValue(key, out var val))
            {
                var unwrapped = Unwrap(val);
                if (unwrapped is string strVal)
                {
                    result = TurkishNormalize(strVal);
                    return true;
                }
                result = unwrapped;
                return true;
            }
            result = null;
            return true;
        }

        public static string TurkishNormalize(string str)
        {
            if (string.IsNullOrEmpty(str)) return string.Empty;
            var sb = new System.Text.StringBuilder(str.Length);
            foreach (var c in str)
            {
                if (c == 'I' || c == 'İ' || c == 'ı' || c == 'i')
                {
                    sb.Append('i');
                }
                else if (c == 'Ö' || c == 'ö')
                {
                    sb.Append('ö');
                }
                else if (c == 'Ü' || c == 'ü')
                {
                    sb.Append('ü');
                }
                else if (c == 'Ç' || c == 'ç')
                {
                    sb.Append('ç');
                }
                else if (c == 'Ş' || c == 'ş')
                {
                    sb.Append('ş');
                }
                else if (c == 'Ğ' || c == 'ğ')
                {
                    sb.Append('ğ');
                }
                else
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            return sb.ToString();
        }
    }
}
