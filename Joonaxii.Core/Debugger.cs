using System.Diagnostics;

namespace Joonaxii
{
    public class Debugger
    {
        public static Debugger Global 
        {
            get => _global ?? _default;
            set => _global = value;
        }

        private static Debugger _global = new Debugger();
        private static Debugger _default = new Debugger();

        public virtual void Log_Impl(string message)
        {
            Debug.Print(message);
        }

        public virtual void LogWarning_Impl(string message)
        {
            Debug.Print(message);
        }

        public virtual void LogError_Impl(string message)
        {
            Debug.Print(message);
        }

        public virtual void Assert_Impl(bool condition, string message)
        {
            Debug.Assert(condition, message);
        }

        public static void Log(string message) => Global.Log_Impl(message);
        public static void LogWarning(string message) => Global.LogWarning_Impl(message);
        public static void LogError(string message) => Global.LogError_Impl(message);

#if DEBUG
        public static void Assert(bool condition, string message) => Global.Assert_Impl(condition, message);
#else
        public static void Assert(bool condition, string message) { }
#endif
    }
}
