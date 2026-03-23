using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class CompanyMappingProfile : Profile
{
    public CompanyMappingProfile()
    {
        CreateMap<CompanyCommandModel, Company>()
            .ForMember(dest => dest.OwnerUserId, opt => opt.Ignore())
            .ForMember(dest => dest.LicenseKey, opt => opt.Ignore())
            .ForMember(dest => dest.LicensedFeatures, opt => opt.Ignore())
            .ForMember(dest => dest.LicenseExpiresOn, opt => opt.Ignore())
            .ForMember(dest => dest.Settings, opt => opt.Ignore());

        CreateMap<Company, CompanyResponse>();
        CreateMap<CompanySettings, CompanySettingsResponse>();
    }
}
