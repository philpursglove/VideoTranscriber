using System.Web;

namespace VideoTranscriberVideoClient;

public class VideoIndexerClientBase
{
    protected const string ApiUrl = "https://api.videoindexer.ai";

    internal string CorrectName(string videoName)
    {
        string correctedName = videoName.Replace(" ", string.Empty);
        if (correctedName.Length > 80)
        {
            correctedName = correctedName.Substring(0, 80);
        }

        return correctedName;
    }

    internal string CreateQueryString(IDictionary<string, string> parameters)
    {
        var queryParameters = HttpUtility.ParseQueryString(string.Empty);
        foreach (var parameter in parameters)
        {
            queryParameters[parameter.Key] = parameter.Value;
        }

        return queryParameters.ToString();
    }
}