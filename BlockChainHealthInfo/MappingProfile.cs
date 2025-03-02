using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlockChainHealthInfo
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Patient mappings
            CreateMap<DbPatient, DbPatient>()
                .ForMember(dest => dest.Id, opt => opt.Ignore()) // Preserve original ID
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) =>
                    srcMember != null && !srcMember.Equals(dest)));

            // Encounter mappings
            CreateMap<DbEncounter, DbEncounter>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.PatientId, opt => opt.Ignore());

            // Observation mappings
            CreateMap<DbObservation, DbObservation>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.EncounterId, opt => opt.Ignore());

            // Add other mappings as needed for your domain objects
            // CreateMap<SourceType, DestinationType>();
        }
    }
}
