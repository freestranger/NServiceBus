namespace NServiceBus.Pipeline
{
    using System;

    /// <summary>
    /// A pipe instance.
    /// </summary>
    public class Pipe
    {
        internal Pipe()
        {
            stepsTracker = new StepsTracker();
        }

        /// <summary>
        /// The pipe instances.
        /// </summary>
        public IObservable<Step> Steps
        {
            get { return stepsTracker; }
        }

        internal void AddStep(Step step)
        {
            stepsTracker.Add(step);
        }

        internal void CompleteSteps()
        {
            stepsTracker.Complete();
        }

        StepsTracker stepsTracker;
    }
}