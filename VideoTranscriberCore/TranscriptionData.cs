﻿using System.ComponentModel.DataAnnotations;

namespace VideoTranscriberCore;

public class TranscriptionData
{
    public Guid id { get; set; }

    public string Language { get; set; }

    public IEnumerable<TranscriptElement> Transcript { get; set; }

    public string OriginalFilename { get; set; }

    public Guid VideoId { get; set; }

    public Guid BatchId { get; set; }

    public double Duration { get; set; }

    public string ProjectName { get; set; }

    public DateTime UploadDate { get; set; }

    public int SpeakerCount { get; set; }

    [DisplayFormat(DataFormatString = "{0:F2}")]
    public double Confidence { get; set; }

    public IEnumerable<string> Keywords { get; set; }

    public IEnumerable<Speaker> Speakers { get; set; }

    public TimeSpan IndexDuration { get; set; }

    public string Owner { get; set; }
    public string SecurityGroup { get; set; }

    public TranscriptionStatus TranscriptionStatus { get; set; }
}