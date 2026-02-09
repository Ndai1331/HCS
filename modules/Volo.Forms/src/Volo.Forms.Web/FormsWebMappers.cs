using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using Volo.Forms.Forms;

namespace Volo.Forms.Web;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FormSettingsDtoToUpdateFormSettingInputDtoMapper : MapperBase<FormSettingsDto, UpdateFormSettingInputDto>
{
    public override partial UpdateFormSettingInputDto Map(FormSettingsDto source);
    public override partial void Map(FormSettingsDto source, UpdateFormSettingInputDto destination);
}