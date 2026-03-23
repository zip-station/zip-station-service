using AutoMapper;
using ZipStation.Models.CommandModels;
using ZipStation.Models.Entities;
using ZipStation.Models.Responses;

namespace ZipStation.Mapping;

public class CustomerMappingProfile : Profile
{
    public CustomerMappingProfile()
    {
        CreateMap<CustomerCommandModel, Customer>();
        CreateMap<Customer, CustomerResponse>();
    }
}
