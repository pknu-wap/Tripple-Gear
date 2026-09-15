using System;
using System.Collections;

[Serializable]
public abstract class SkillLogic
{
    public abstract IEnumerator Execute(SkillContext context);

    public virtual void OnValidate()
    {
    }
}
