using System;
using System.Collections.Generic;

namespace EmailAndADO
{
    public class DepositDO : DataObjectBase
    {
        /// <summary>
        /// Inserta un deposito con impacto en boveda
        /// </summary>
        public bool InsertWithVaultMaster(VDepositInsert DepositToInsert, int IdTAuditClosure)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(new dbParameter(VaultMaster.kVKTVaultType, DepositToInsert.VKTVaultType));
            parameterList.Add(new dbParameter(VDepositInsert.kFKTVault, DepositToInsert.FKTVault));
            parameterList.Add(new dbParameter(VDepositInsert.kFKTUserApprover, DepositToInsert.FKTUserApprover));
            parameterList.Add(new dbParameter(VDepositInsert.kFKTVaultMasterLot, DepositToInsert.FKTVaultMasterLot));

            parameterList.Add(new dbParameter(VDepositInsert.kVKDeposit, DepositToInsert.VKDeposit));
            parameterList.Add(new dbParameter("VKDepositDaily", DepositType.VaultDaily));
            parameterList.Add(new dbParameter("VKDepositCreditCardComission", DepositType.CardsComission));
            parameterList.Add(new dbParameter(VDepositInsert.kComments, DepositToInsert.Comments));

            parameterList.Add(new dbParameter(VDepositInsert.kDepositNumberLocal, DepositToInsert.DepositNumberLocal));
            parameterList.Add(new dbParameter(VDepositInsert.kDepositNumberDollar, DepositToInsert.DepositNumberDollar));
            parameterList.Add(new dbParameter(VDepositInsert.kBagNumber, DepositToInsert.BagNumber));
            parameterList.Add(new dbParameter(VDepositInsert.kManifestNumber, DepositToInsert.ManifestNumber));
            parameterList.Add(new dbParameter(VDepositInsert.kAmmountLocal, DepositToInsert.AmmountLocal));
            parameterList.Add(new dbParameter(VDepositInsert.kAmmountDollar, DepositToInsert.AmmountDollar));

            parameterList.Add(new dbParameter("VKCurrencyDenominationBills", CurrencyDenominationType.Bills));
            parameterList.Add(new dbParameter("VKCurrencyDenominationCoins", CurrencyDenominationType.Coins));
            parameterList.Add(new dbParameter("VKCurrencyDenominationFillSets", CurrencyDenominationType.FillSets));

            parameterList.Add(new dbParameter(VDepositInsert.kLstBillsDB, DepositToInsert.LstBillsDB));
            parameterList.Add(new dbParameter(VDepositInsert.kLstCoinsDB, DepositToInsert.LstCoinsDB));
            parameterList.Add(new dbParameter(VDepositInsert.kLstFillSetsDB, DepositToInsert.LstFillSetsDB));

            parameterList.Add(new dbParameter(VDepositInsert.kLstBillsDollarDB, DepositToInsert.LstBillsDollarDB));
            parameterList.Add(new dbParameter(VDepositInsert.kLstCoinsDollarDB, DepositToInsert.LstCoinsDollarDB));
            parameterList.Add(new dbParameter(VDepositInsert.kLstFillSetsDollarDB, DepositToInsert.LstFillSetsDollarDB));

            parameterList.Add(new dbParameter(AuditClosure.kIdTAuditClosure, IdTAuditClosure));

            return ExecuteProcedure("FIN.uspTDepositInsertWithVaultMaster", parameterList);
        }

        /// <summary>
        /// Selecciona los depositos con impacto en boveda para una fecha
        /// </summary>
        public List<VDeposit> SelectAllForDateWithVaultMaster(int FKTBranch, DateTime CasinoDate)
        {
            string CasinoDateDB = Converter.ToDataBaseString(CasinoDate);

            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(new dbParameter(CasinoDay.kFKTBranch, FKTBranch));
            parameterList.Add(new dbParameter(CasinoDay.kCasinoDate, CasinoDateDB));

            LoadOpenReader("FIN.uspTDepositSelectAllForDateWithVaultMaster", parameterList);

            List<VDeposit> depositList = new List<VDeposit>();

            try
            {
                while (reader.Read())
                {
                    VDeposit curDeposit = new VDeposit();
                    curDeposit.IdTDeposit = reader.GetInt32(0);
                    curDeposit.Num = reader.GetInt32(1);
                    curDeposit.DepositNumberLocal = reader.GetString(2);
                    curDeposit.DepositNumberDollar = reader.GetString(3);
                    curDeposit.AmmountLocal = reader.GetDecimal(4);
                    curDeposit.AmmountDollar = reader.GetDecimal(5);
                    curDeposit.BagNumber = reader.GetString(6);
                    curDeposit.ManifestNumber = reader.GetString(7);
                    curDeposit.AccountNumberLocal = reader.GetString(8);
                    curDeposit.AccountNumberDollar = reader.GetString(9);
                    curDeposit.Comments = reader.GetString(10);
                    curDeposit.VKDeposit = reader.GetInt32(11);
                    curDeposit.FKTVaultMaster = reader.GetInt32(12);

                    depositList.Add(curDeposit);
                }

                CloseConnection();
            }
            catch (Exception e)
            {
                InsertLog("VaultMasterDO: " + e.Message);
                depositList = null;
            }

            return depositList;
        }

        /// <summary>
        /// Inserta un deposito para un conteo
        /// </summary>
        public bool InsertWithDailyLot(VDepositInsert DepositToInsert, int FKTDailyLot)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(new dbParameter(DepositXTDailyLot.kFKTDailyLot, FKTDailyLot));

            parameterList.Add(new dbParameter(VDepositInsert.kFKTUser, DepositToInsert.FKTUser));
            parameterList.Add(new dbParameter(VDepositInsert.kVKDeposit, DepositToInsert.VKDeposit));
            parameterList.Add(new dbParameter(VDepositInsert.kComments, DepositToInsert.Comments));

            parameterList.Add(new dbParameter(VDepositInsert.kDepositNumberLocal, DepositToInsert.DepositNumberLocal));
            parameterList.Add(new dbParameter(VDepositInsert.kDepositNumberDollar, DepositToInsert.DepositNumberDollar));
            parameterList.Add(new dbParameter(VDepositInsert.kBagNumber, DepositToInsert.BagNumber));
            parameterList.Add(new dbParameter(VDepositInsert.kManifestNumber, DepositToInsert.ManifestNumber));
            parameterList.Add(new dbParameter(VDepositInsert.kAmmountLocal, DepositToInsert.AmmountLocal));
            parameterList.Add(new dbParameter(VDepositInsert.kAmmountDollar, DepositToInsert.AmmountDollar));

            return ExecuteProcedure("FIN.uspTDepositInsertWithDailyLot", parameterList);
        }

        /// <summary>
        /// Inserta un deposito simple
        /// </summary>
        public bool Insert(VDepositInsert DepositToInsert)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(new dbParameter(VDepositInsert.kCasinoDate, DepositToInsert.CasinoDate));
            parameterList.Add(new dbParameter(VDepositInsert.kFKTBranch, DepositToInsert.FKTBranch));

            parameterList.Add(new dbParameter(VDepositInsert.kFKTUser, DepositToInsert.FKTUser));
            parameterList.Add(new dbParameter(VDepositInsert.kVKDeposit, DepositToInsert.VKDeposit));
            parameterList.Add(new dbParameter(VDepositInsert.kComments, DepositToInsert.Comments));

            parameterList.Add(new dbParameter(VDepositInsert.kDepositNumberLocal, DepositToInsert.DepositNumberLocal));
            parameterList.Add(new dbParameter(VDepositInsert.kDepositNumberDollar, DepositToInsert.DepositNumberDollar));
            parameterList.Add(new dbParameter(VDepositInsert.kBagNumber, DepositToInsert.BagNumber));
            parameterList.Add(new dbParameter(VDepositInsert.kManifestNumber, DepositToInsert.ManifestNumber));

            parameterList.Add(new dbParameter(VDepositInsert.kAmmountLocal, DepositToInsert.AmmountLocal));
            parameterList.Add(new dbParameter(VDepositInsert.kAmmountDollar, DepositToInsert.AmmountDollar));

            return ExecuteProcedure("FIN.uspTDepositInsert", parameterList);
        }

        /// <summary>
        /// Deshabilita un depósito
        /// </summary>
        public bool Disable(int IdTDeposit)
        {
            List<dbParameter> parameterList = new List<dbParameter>();
            parameterList.Add(new dbParameter(Deposit.kIdTDeposit, IdTDeposit));

            return ExecuteProcedure("FIN.uspTDepositDisable", parameterList);
        }

    }
}
