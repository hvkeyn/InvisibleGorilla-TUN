using System;
using System.Threading;

namespace InvisibleGorillaTUN.Foundation
{
    public class Scheduler
    {
        public void WaitUntil(Func<bool> condition, int millisecondsTimeout, string timeoutError)
        {
            if (condition.Invoke())
                return;

            const int sliceMs = 100;
            int elapsed = 0;
            while (elapsed < millisecondsTimeout)
            {
                Thread.Sleep(sliceMs);
                elapsed += sliceMs;

                if (condition.Invoke())
                    return;
            }

            throw new Exception(timeoutError);
        }
    }
}
