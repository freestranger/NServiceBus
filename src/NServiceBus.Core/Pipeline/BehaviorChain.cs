namespace NServiceBus.Pipeline
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.ExceptionServices;

    class BehaviorChain<T> where T : BehaviorContext
    {
        readonly PipelineExecutor pipelineExecutor;
        Queue<Type> itemDescriptors = new Queue<Type>();
        Stack<Queue<Type>> snapshots = new Stack<Queue<Type>>();
        ExceptionDispatchInfo preservedRootException;
        Pipe pipe;

        public BehaviorChain(IEnumerable<Type> behaviorList, PipelineExecutor pipelineExecutor)
        {
            this.pipelineExecutor = pipelineExecutor;
            foreach (var behaviorType in behaviorList)
            {
                itemDescriptors.Enqueue(behaviorType);
            }
        }

        public void Invoke(T context)
        {
            try
            {
                context.SetChain(this);
                pipe = new Pipe();
                pipelineExecutor.AddNewInstance(pipe);
                InvokeNext(context);
            }
            catch
            {
                if (preservedRootException != null)
                {
                    preservedRootException.Throw();
                }

                throw;
            }
        }

        void InvokeNext(T context)
        {
            if (itemDescriptors.Count == 0)
            {
                pipe.CompleteSteps();
                return;
            }

            var behaviorType = itemDescriptors.Dequeue();

            try
            {
                var instance = (IBehavior<T>)context.Builder.Build(behaviorType);
                var step = new Step { Behavior = behaviorType, Id = "stepId" };
                pipe.AddStep(step);
                var watch = Stopwatch.StartNew();
                instance.Invoke(context, () => InvokeNext(context));
                watch.Stop();
                step.Duration = watch.Elapsed;
            }
            catch (Exception exception)
            {
                if (preservedRootException == null)
                {
                    preservedRootException = ExceptionDispatchInfo.Capture(exception);
                }

                pipe.CompleteSteps();

                throw;
            }
        }

        public void TakeSnapshot()
        {
            snapshots.Push(new Queue<Type>(itemDescriptors));
        }

        public void DeleteSnapshot()
        {
            itemDescriptors = new Queue<Type>(snapshots.Pop());
        }
    }
}