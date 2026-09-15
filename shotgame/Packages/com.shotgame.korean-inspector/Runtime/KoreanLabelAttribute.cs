using System;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class KoreanLabelAttribute : Attribute
{
    public KoreanLabelAttribute(string label)
    {
        Label = label;
    }

    public string Label { get; }
}
