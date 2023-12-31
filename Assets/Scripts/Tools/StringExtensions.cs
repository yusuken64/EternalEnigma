public static class StringExtensions
{
    public static string TrimSuffix(this string str, string suffix)
    {
        if (str.EndsWith(suffix))
        {
            return str.Substring(0, str.Length - suffix.Length);
        }
        return str;
    }
}