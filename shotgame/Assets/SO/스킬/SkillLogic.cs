using System;
using System.Collections;

[Serializable]
public abstract class SkillLogic
{
    public virtual bool RequiresAimDirection => false;
    public virtual bool ActivatesOnPickup => false;

    public virtual bool CanExecute(SkillContext context) => true;

    public abstract IEnumerator Execute(SkillContext context);

    public virtual bool TryApplyOnPickup(ArisaHealth health)
    {
        return false;
    }

    public virtual bool TryApplyOnPickup(ArisaSkillController controller)
    {
        return controller != null && TryApplyOnPickup(controller.GetComponent<ArisaHealth>());
    }

    public virtual void OnValidate()
    {
    }
}
