namespace NServiceBus.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Contexts;
    using Janitor;
    using ObjectBuilder;
    using Settings;
    using Unicast;
    using Unicast.Messages;

    /// <summary>
    /// Orchestrates the execution of a pipeline.
    /// </summary>
    public class PipelineExecutor : IDisposable
    {
        /// <summary>
        /// Create a new instance of <see cref="PipelineExecutor"/>.
        /// </summary>
        /// <param name="settings">The settings to read data from.</param>
        /// <param name="builder">The builder.</param>
        public PipelineExecutor(ReadOnlySettings settings, IBuilder builder)
        {
            rootBuilder = builder;

            var pipelineBuilder = new PipelineBuilder(settings.Get<PipelineModifications>());
            Incoming = pipelineBuilder.Incoming.AsReadOnly();
            Outgoing = pipelineBuilder.Outgoing.AsReadOnly();

            incomingBehaviors = Incoming.Select(r => r.BehaviorType);
            outgoingBehaviors = Outgoing.Select(r => r.BehaviorType);
        }

        /// <summary>
        /// The list of incoming steps registered.
        /// </summary>
        public IList<RegisterStep> Incoming { get; private set; }
        
        /// <summary>
        /// The list of outgoing steps registered.
        /// </summary>
        public IList<RegisterStep> Outgoing { get; private set; }

        public IObservable<PipelineInstance> Instances { get { return pipelineInstances; } } 

        /// <summary>
        /// The current context being executed.
        /// </summary>
        public BehaviorContext CurrentContext
        {
            get
            {
                var current = contextStacker.Current;

                if (current != null)
                {
                    return current;
                }

                contextStacker.Push(new RootContext(rootBuilder));

                return contextStacker.Current;
            }
        }

        /// <summary>
        /// Invokes a chain of behaviors. 
        /// </summary>
        /// <typeparam name="TContext">The context to use.</typeparam>
        /// <param name="behaviors">The behaviors to execute in the specified order.</param>
        /// <param name="context">The context instance.</param>
        public void InvokePipeline<TContext>(IEnumerable<Type> behaviors, TContext context) where TContext : BehaviorContext
        {
            var pipeline = new BehaviorChain<TContext>(behaviors, this);

            Execute(pipeline, context);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        /// <filterpriority>2</filterpriority>
        public void Dispose()
        {
            //Injected
        }

        internal void PreparePhysicalMessagePipelineContext(TransportMessage message)
        {
            contextStacker.Push(new IncomingContext(CurrentContext, message));
        }

        internal void InvokeReceivePhysicalMessagePipeline()
        {
            var context = contextStacker.Current as IncomingContext;

            if (context == null)
            {
                throw new InvalidOperationException("Can't invoke the receive pipeline when the current context is: " + contextStacker.Current.GetType().Name);
            }

            InvokePipeline(incomingBehaviors, context);
        }

        internal void CompletePhysicalMessagePipelineContext()
        {
            contextStacker.Pop();
        }

        internal OutgoingContext InvokeSendPipeline(DeliveryOptions deliveryOptions, LogicalMessage message)
        {
            var context = new OutgoingContext(CurrentContext, deliveryOptions, message);

            InvokePipeline(outgoingBehaviors, context);

            return context;
        }

        void DisposeManaged()
        {
            contextStacker.Dispose();
        }

        void Execute<T>(BehaviorChain<T> pipelineAction, T context) where T : BehaviorContext
        {
            try
            {
                contextStacker.Push(context);

                pipelineAction.Invoke(context);
            }
            finally
            {
                contextStacker.Pop();
            }
        }

        BehaviorContextStacker contextStacker = new BehaviorContextStacker();
        IBuilder rootBuilder;
        IEnumerable<Type> incomingBehaviors;
        IEnumerable<Type> outgoingBehaviors;
        PipelineInstanceTracker pipelineInstances = new PipelineInstanceTracker();

        public void AddNewInstance(PipelineInstance instance)
        {
            pipelineInstances.Add(instance);
        }
    }

    public class PipelineInstance
    {
        StepsTracker stepsTracker;

        public PipelineInstance()
        {
            stepsTracker = new StepsTracker();
        }

        public IObservable<Step> Steps { get { return stepsTracker; } }

        public void AddStep(Step step)
        {
            stepsTracker.Add(step);
        }

        public void CompleteSteps()
        {
            stepsTracker.Complete();
        }
    }

    class PipelineInstanceTracker : IObservable<PipelineInstance>
    {
        private List<IObserver<PipelineInstance>> observers;

        public PipelineInstanceTracker()
        {
            observers = new List<IObserver<PipelineInstance>>();
        }

        public IDisposable Subscribe(IObserver<PipelineInstance> observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }

            return new Unsubscriber(observers, observer);
        }

        public void Add(PipelineInstance instance)
        {
            foreach (var observer in observers)
            {
                observer.OnNext(instance);
            }
        }

        [SkipWeaving]
        private class Unsubscriber : IDisposable
        {
            private List<IObserver<PipelineInstance>> observers;
            private IObserver<PipelineInstance> observer;

            public Unsubscriber(List<IObserver<PipelineInstance>> observers, IObserver<PipelineInstance> observer)
            {
                this.observers = observers;
                this.observer = observer;
            }

            public void Dispose()
            {
                if (observer != null && observers.Contains(observer))
                {
                    observers.Remove(observer);
                }
            }
        }
    }

    public struct Step
    {
        public string Id { get; set; }
        public Type Behavior { get; set; }
        public TimeSpan Duration { get; set; }
    }

    class StepsTracker: IObservable<Step>
    {
        private List<IObserver<Step>> observers;

        public StepsTracker()
        {
            observers = new List<IObserver<Step>>();
        }
        public IDisposable Subscribe(IObserver<Step> observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }

            return new Unsubscriber(observers, observer);
        }

        public void Add(Step step)
        {
            foreach (var observer in observers)
            {
                observer.OnNext(step);
            }
        }

        public void Complete()
        {
            foreach (var observer in observers)
            {
                observer.OnCompleted();
            }
        }

        [SkipWeaving]
        private class Unsubscriber : IDisposable
        {
            private List<IObserver<Step>> observers;
            private IObserver<Step> observer;

            public Unsubscriber(List<IObserver<Step>> observers, IObserver<Step> observer)
            {
                this.observers = observers;
                this.observer = observer;
            }

            public void Dispose()
            {
                if (observer != null && observers.Contains(observer))
                {
                    observers.Remove(observer);
                }
            }
        }
    }
}