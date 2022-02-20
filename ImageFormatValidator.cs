using System.Text.RegularExpressions;

public class ImageFormatValidator
{
    public bool IsValid(string filename) =>
        Regex.IsMatch(new FileInfo(filename).Extension, @"\.jpe?g", RegexOptions.IgnoreCase);
}


