using System;

namespace Volo.Forms.Questions;

[System.Reflection.Obfuscation(Exclude = true, Feature = Volo.Abp.Commercial.Core.Obfuscation.Feature)]
public class ShortText : QuestionBase, IRequired
{
    public bool IsRequired { get; set; }

    protected ShortText()
    {
    }

    public ShortText(Guid id, Guid? tenantId = null) : base(id, tenantId)
    {
    }

    public override QuestionTypes GetQuestionType()
    {
        return QuestionTypes.ShortText;
    }
}
