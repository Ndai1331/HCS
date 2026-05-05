using System;

namespace HC.Reports;

public abstract class ReportDownloadTokenCacheItemBase
{
    public string Token { get; set; } = null!;
}