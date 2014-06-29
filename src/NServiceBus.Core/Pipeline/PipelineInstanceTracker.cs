namespace NServiceBus.Pipeline
{
    using System;
    using System.Collections.Generic;
    using Janitor;

    class PipelineInstanceTracker : IObservable<Pipe>
    {
        public PipelineInstanceTracker()
        {
            observers = new List<IObserver<Pipe>>();
        }

        public IDisposable Subscribe(IObserver<Pipe> observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }

            return new Unsubscriber(observers, observer);
        }

        public void Add(Pipe instance)
        {
            foreach (var observer in observers)
            {
                observer.OnNext(instance);
            }
        }

        List<IObserver<Pipe>> observers;

        [SkipWeaving]
        class Unsubscriber : IDisposable
        {
            public Unsubscriber(List<IObserver<Pipe>> observers, IObserver<Pipe> observer)
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

            IObserver<Pipe> observer;
            List<IObserver<Pipe>> observers;
        }
    }
}