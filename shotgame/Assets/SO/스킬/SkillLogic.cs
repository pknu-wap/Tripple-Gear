using System;
using System.Collections;

[Serializable]
public abstract class SkillLogic
{
    public virtual bool RequiresAimDirection => false;
    public virtual bool ActivatesOnPickup => false;

    public abstract IEnumerator Execute(SkillContext context);

    public virtual bool TryApplyOnPickup(ArisaHealth health)
    {
        return false;
    }

    public virtual void OnValidate()
    {
    }
}
