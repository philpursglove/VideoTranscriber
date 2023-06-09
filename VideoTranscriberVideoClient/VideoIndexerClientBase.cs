namespace VideoTranscriberVideoClient;

public class VideoIndexerClientBase
{
    internal string CorrectName(string videoName)
    {
        string correctedName = videoName.Replace(" ", string.Empty);
        if (correctedName.Length > 80)
        {
            correctedName = correctedName.Substring(0, 80);
        }

        return correctedName;
    }

}