using System;
using System.Collections.Generic;
using System.Linq;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using Volo.Forms.Answers;
using Volo.Forms.Choices;
using Volo.Forms.Forms;
using Volo.Forms.Questions;
using Volo.Forms.Questions.ChoosableItems;
using Volo.Forms.Responses;

namespace Volo.Forms;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FormResponseToFormResponseDtoMapper : MapperBase<FormResponse, FormResponseDto>
{
    [MapperIgnoreTarget(nameof(FormResponseDto.Answers))]
    public override partial FormResponseDto Map(FormResponse source);

    [MapperIgnoreTarget(nameof(FormResponseDto.Answers))]
    public override partial void Map(FormResponse source, FormResponseDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FormWithResponseToFormWithResponseDtoMapper : MapperBase<FormWithResponse, FormWithResponseDto>
{
    public override partial FormWithResponseDto Map(FormWithResponse source);

    public override partial void Map(FormWithResponse source, FormWithResponseDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class AnswerToAnswerDtoMapper : MapperBase<Answer, AnswerDto>
{
    public override partial AnswerDto Map(Answer source);

    public override partial void Map(Answer source, AnswerDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class ChoiceToChoiceDtoMapper : MapperBase<Choice, ChoiceDto>
{
    public override partial ChoiceDto Map(Choice source);

    public override partial void Map(Choice source, ChoiceDto destination);
}

// [Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
// [MapExtraProperties]
// public partial class ChoiceDtoToChoiceMapper : MapperBase<ChoiceDto, Choice>
// {
//     [MapperIgnoreTarget(nameof(Choice.Id))]
//     [MapperIgnoreTarget(nameof(Choice.TenantId))]
//     [MapperIgnoreTarget(nameof(Choice.ChoosableQuestionId))]
//     public override partial Choice Map(ChoiceDto source);
//
//     public override partial void Map(ChoiceDto source, Choice destination);
// }
//


[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class QuestionBaseToQuestionDtoMapper : MapperBase<QuestionBase, QuestionDto>
{
    [MapperIgnoreTarget(nameof(QuestionDto.IsRequired))]
    [MapperIgnoreTarget(nameof(QuestionDto.HasOtherOption))]
    [MapperIgnoreTarget(nameof(QuestionDto.Choices))]
    public override partial QuestionDto Map(QuestionBase source);

    [MapperIgnoreTarget(nameof(QuestionDto.IsRequired))]
    [MapperIgnoreTarget(nameof(QuestionDto.HasOtherOption))]
    [MapperIgnoreTarget(nameof(QuestionDto.Choices))]
    public override partial void Map(QuestionBase source, QuestionDto destination);
    
    public partial ChoiceDto Map(Choice source);
    public partial List<ChoiceDto> Map(IEnumerable<Choice> source);
    
    public override void AfterMap(QuestionBase source, QuestionDto destination)
    {
       destination.IsRequired = (source as IRequired)?.IsRequired ?? false;
       destination.HasOtherOption = (source as IHasOtherOption)?.HasOtherOption ?? false;
       destination.Choices = Map( (source as IChoosable)?.GetChoices().OrderBy(t => t.Index).ToList()) ?? [];
       destination.QuestionType = source.GetQuestionType();
    }
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FormToFormSettingsDtoMapper : MapperBase<Form, FormSettingsDto>
{
    public override partial FormSettingsDto Map(Form source);

    public override partial void Map(Form source, FormSettingsDto destination);

}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FormToFormDtoMapper : MapperBase<Form, FormDto>
{
    public override partial FormDto Map(Form source);

    public override partial void Map(Form source, FormDto destination);

}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FormWithQuestionsToFormWithDetailsDtoMapper : MapperBase<FormWithQuestions, FormWithDetailsDto>
{
    public override partial FormWithDetailsDto Map(FormWithQuestions source);

    public override partial void Map(FormWithQuestions source, FormWithDetailsDto destination);
    public override void AfterMap(FormWithQuestions source, FormWithDetailsDto destination)
    {
        destination.Description = source.Form.Description;
        destination.Title = source.Form.Title;
        destination.Id = source.Form.Id;
        destination.CreationTime = source.Form.CreationTime;
        destination.TenantId = source.Form.TenantId;
        destination.DeleterId = source.Form.DeleterId;
        destination.CreatorId = source.Form.CreatorId;
        destination.DeletionTime = source.Form.DeletionTime;
        destination.IsDeleted = source.Form.IsDeleted;
        destination.LastModificationTime = source.Form.LastModificationTime;
        destination.LastModifierId = source.Form.LastModifierId;
        destination.IsQuiz = source.Form.IsQuiz;
        destination.IsCollectingEmail = source.Form.IsCollectingEmail;
        destination.CanEditResponse = source.Form.CanEditResponse;
        destination.IsAcceptingResponses = source.Form.IsAcceptingResponses;
        destination.HasLimitOneResponsePerUser = source.Form.HasLimitOneResponsePerUser;
        destination.RequiresLogin = source.Form.RequiresLogin;
    }
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FormToFormWithAnswersDtoMapper : MapperBase<Form, FormWithAnswersDto>
{
    [MapperIgnoreTarget(nameof(FormWithAnswersDto.Questions))]
    public override partial FormWithAnswersDto Map(Form source);

    [MapperIgnoreTarget(nameof(FormWithAnswersDto.Questions))]
    public override partial void Map(Form source, FormWithAnswersDto destination);

}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class QuestionBaseToQuestionWithAnswersDtoMapper : MapperBase<QuestionBase, QuestionWithAnswersDto>
{
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.Answers))]
    [MapperIgnoreTarget(nameof(QuestionDto.IsRequired))]
    [MapperIgnoreTarget(nameof(QuestionDto.HasOtherOption))]
    [MapperIgnoreTarget(nameof(QuestionDto.Choices))]
    public override partial QuestionWithAnswersDto Map(QuestionBase source);

    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.Answers))]
    [MapperIgnoreTarget(nameof(QuestionDto.IsRequired))]
    [MapperIgnoreTarget(nameof(QuestionDto.HasOtherOption))]
    [MapperIgnoreTarget(nameof(QuestionDto.Choices))]
    public override partial void Map(QuestionBase source, QuestionWithAnswersDto destination);
    public partial ChoiceDto Map(Choice source);
    public partial List<ChoiceDto> Map(IEnumerable<Choice> source);
    public override void AfterMap(QuestionBase source, QuestionWithAnswersDto destination)
    {
        destination.IsRequired = (source as IRequired)?.IsRequired ?? false;
        destination.HasOtherOption = (source as IHasOtherOption)?.HasOtherOption ?? false;
        destination.Choices = Map( (source as IChoosable)?.GetChoices().OrderBy(t => t.Index).ToList()) ?? [];;
    }
}


[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class QuestionWithAnswersToQuestionWithAnswersDtoMapper : MapperBase<QuestionWithAnswers, QuestionWithAnswersDto>
{
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.CreationTime))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.CreatorId))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.LastModificationTime))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.LastModifierId))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.IsDeleted))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.DeletionTime))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.DeleterId))]
    public override partial QuestionWithAnswersDto Map(QuestionWithAnswers source);

    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.CreationTime))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.CreatorId))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.LastModificationTime))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.LastModifierId))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.IsDeleted))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.DeletionTime))]
    [MapperIgnoreTarget(nameof(QuestionWithAnswersDto.DeleterId))]
    public override partial void Map(QuestionWithAnswers source, QuestionWithAnswersDto destination);

    public override void AfterMap(QuestionWithAnswers source, QuestionWithAnswersDto destination)
    {
        destination.Id = source.Question.Id;
        destination.Index = source.Question.Index;
        destination.Title = source.Question.Title;
        destination.Description = source.Question.Description;
        destination.IsRequired = (source.Question as IRequired)?.IsRequired ?? false;
        destination.HasOtherOption = (source.Question as IHasOtherOption)?.HasOtherOption ?? false;
        destination.QuestionType = source.Question.GetQuestionType();
        destination.FormId = source.Question.FormId;
    }
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FormResponseToFormResponseDetailedDtoMapper : MapperBase<FormResponse, FormResponseDetailedDto>
{
    public override partial FormResponseDetailedDto Map(FormResponse source);

    public override partial void Map(FormResponse source, FormResponseDetailedDto destination);

}

