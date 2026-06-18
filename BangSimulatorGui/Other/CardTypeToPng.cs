using System;
using System.Collections.Generic;
using System.Text;
using BangSimulatorLib.Game;

namespace BangSimulatorGui.Other
{
    public class CardTypeToPng
    {
        public static string GetCardPng(CardBangType cardType)
        {
            string baseP = "Assets/Cards/";

            switch (cardType)
            {
                case CardBangType.Barrel: return baseP + "barile.png";
                case CardBangType.scope: return baseP + "hledi_mirino.png";
                case CardBangType.Mustang: return baseP + "mustang.png";
                case CardBangType.Dinamite: return baseP + "dinamite.png";
                case CardBangType.Jail: return baseP + "prigione.png";
                case CardBangType.Remington: return baseP + "remington.png";
                case CardBangType.Carabine: return baseP + "rev_carabine.png";
                case CardBangType.Schofield: return baseP + "schofield.png";
                case CardBangType.Vulcanic: return baseP + "volcanic.png";
                case CardBangType.Winchester: return baseP + "winchester.png";
                case CardBangType.Bang: return baseP + "bang.png";
                case CardBangType.Beer: return baseP + "pivo.png";
                case CardBangType.CatBalou: return baseP + "cat_balou.png";
                case CardBangType.Duel: return baseP + "duel.png";
                case CardBangType.Gatling: return baseP + "gatling.png";
                case CardBangType.GeneralStore: return baseP + "emporio.png";
                case CardBangType.Indians: return baseP + "indiani.png";
                case CardBangType.Missed: return baseP + "vedla_mancato.png";
                case CardBangType.Panic: return baseP + "panico.png";
                case CardBangType.Salon: return baseP + "saloon.png";
                case CardBangType.Stagecoach: return baseP + "diligenza.png";
                case CardBangType.WellsFargo: return baseP + "wells_fargo.png";

                default: return string.Empty;
            }
        }

        public static string GetRolesPng(PlayerRole role)
        {
            string baseP = "Assets/Roles/";

            switch(role)
            {
                case PlayerRole.Renegade: return baseP + "rinnegato.png";
                case PlayerRole.Sheriff: return baseP + "sceriffo.png";
                case PlayerRole.Bandit: return baseP + "fuorilegge.png";
                case PlayerRole.Deputy: return baseP + "vice.png";

                default: return string.Empty;
            }
        }
    }
}
