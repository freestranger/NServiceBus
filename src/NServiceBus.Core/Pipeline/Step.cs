namespace NServiceBus.Pipeline
{
    using System;

    /// <summary>
    /// Pipeline step
    /// </summary>
    public struct Step
    {
        /// <summary>
        /// Step identifier.
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        /// Behavior type.
        /// </summary>
        public Type Behavior { get; internal set; }
        
        /// <summary>
        /// Execution time.
        /// </summary>
        public TimeSpan Duration { get; internal set; }
    }
}