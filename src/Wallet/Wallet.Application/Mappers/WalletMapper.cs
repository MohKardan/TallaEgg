using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Mappers;
using Wallet.Core;

namespace Wallet.Application.Mappers
{
    public class WalletMapper : BaseMapper<WalletEntity, WalletDTO>
    {
        public override WalletDTO Map(WalletEntity entity)
        {
            if (entity == null) return null;

            return new WalletDTO
            {
                Asset = entity.Asset,
                Balance = entity.Balance,
                LockedBalance = entity.LockedBalance,
                UpdatedAt = entity.UpdatedAt    
            };
        }

        public IEnumerable<WalletDTO> Map(IEnumerable<WalletEntity> entities)
        {
            List<WalletDTO> walletDTOs = new List<WalletDTO>();
            foreach (var entity in entities)
            { 
                walletDTOs.Add(Map(entity));
            }
            return walletDTOs;
        }

        public override WalletEntity MapBack(WalletDTO dto)
        {
            throw new NotImplementedException();
        }
    }
}
