using System.Collections;

namespace CustomPosters
{

    public class CoroutineTask<T> : IEnumerator
    {
        private readonly IEnumerator coroutine;

        private readonly TaskResult<T> result;

        public object Current => coroutine.Current;

        public CoroutineTask(IEnumerator coroutine, TaskResult<T> result)
        {
            this.coroutine = coroutine;
            this.result = result;
        }

        public T GetResult()
        {
            return result.Get();
        }

        public bool MoveNext()
        {
            return coroutine.MoveNext();
        }

        public void Reset()
        {
            coroutine.Reset();
        }
    }
    public class TaskResult<T> : IOut<T>
    {
        private T value;

        public virtual void Set(T value)
        {
            this.value = value;
        }

        public T Get()
        {
            return value;
        }
    }
    public interface IOut<T>
    {
        void Set(T value);
    }
}