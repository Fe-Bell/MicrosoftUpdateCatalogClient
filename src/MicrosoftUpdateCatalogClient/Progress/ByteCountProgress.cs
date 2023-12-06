using System;

namespace MicrosoftUpdateCatalogClient.Progress
{
    /// <summary>
    /// Progress implementation that counts bytes.
    /// </summary>
    public sealed class ByteCountProgress : 
        IProgress<long>
    {
        public long Count { get; set; } = 0;

        public long TotalSize { get; set; } = 0;

        public event EventHandler<EventArgs> OnCountUpdated;

        public ByteCountProgress()
        {
            
        }

        public double GetPercentage()
            => GetRatio() * 100.0d;

        public double GetRatio()
            => (double)Count / TotalSize;

        public bool IsComplete()
            => Count >= TotalSize;

        public void Report(long value)
        {
            if (IsComplete())
                return;

            Count += value;
            OnCountUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
