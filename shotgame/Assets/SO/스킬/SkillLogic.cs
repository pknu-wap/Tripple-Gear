using System;
using System.Collections;

[Serializable]
public abstract class SkillLogic
{
    public virtual bool RequiresAimDirection => false;

    public abstract IEnumerator Execute(SkillContext context);

    public virtual void OnValidate()
    {
    }
}
