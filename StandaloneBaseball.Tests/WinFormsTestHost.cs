using System.Reflection;
using System.Runtime.ExceptionServices;

namespace StandaloneBaseball.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WinFormsTestCollection
{
    public const string Name = "WinForms workflows";
}

internal static class WinFormsTestHost
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static void Run(Action action)
    {
        Exception failure = null;
        using var completed = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                completed.Set();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        Assert.True(completed.Wait(TimeSpan.FromSeconds(30)), "WinForms test exceeded 30 seconds.");
        if (failure != null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public static T Field<T>(object target, string name)
    {
        var field = target.GetType().GetField(name, InstanceFlags);
        Assert.NotNull(field);
        return (T)field.GetValue(target);
    }

    public static object Invoke(object target, string name, params object[] arguments)
        => InvokeMethod(target.GetType().GetMethod(name, InstanceFlags), target, arguments);

    public static object InvokeStatic(Type type, string name, params object[] arguments)
        => InvokeMethod(type.GetMethod(name, StaticFlags), null, arguments);

    private static object InvokeMethod(MethodInfo method, object target, object[] arguments)
    {
        Assert.NotNull(method);
        try
        {
            return method.Invoke(target, arguments);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
}
