namespace Gyurma;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public class GyurmaAttribute : Attribute
{
    public Type Type { get; }

    public GyurmaAttribute(Type type)
    {
        this.Type = type;
    }
}
