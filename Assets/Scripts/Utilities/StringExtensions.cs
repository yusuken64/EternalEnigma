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

    public static string TrimPrefix(this string str, string prefix)
    {
        if (str.StartsWith(prefix))
        {
            return str.Substring(prefix.Length);
        }
        return str;
    }
}