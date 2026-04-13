/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;

#nullable disable

namespace OCPP.Core.Database
{
    public partial class OCPPCoreContext : DbContext
    {
        public OCPPCoreContext(DbContextOptions<OCPPCoreContext> options)
            : base(options)
        {
        }

        public virtual DbSet<ChargePoint> ChargePoints { get; set; }
        public virtual DbSet<ChargeTag> ChargeTags { get; set; }
        public virtual DbSet<ConnectorStatus> ConnectorStatuses { get; set; }
        public virtual DbSet<MessageLog> MessageLogs { get; set; }
        public virtual DbSet<Transaction> Transactions { get; set; }
        
        public virtual DbSet<EVCDTO.Users> Users { get; set; }
        public virtual DbSet<EVCDTO.ChargingHub> ChargingHubs { get; set; }
        public virtual DbSet<EVCDTO.ChargingHubReview> ChargingHubReviews { get; set; }
        public virtual DbSet<EVCDTO.ChargingSession> ChargingSessions { get; set; }
        public virtual DbSet<EVCDTO.ChargingStation> ChargingStations { get; set; }
        public virtual DbSet<EVCDTO.ChargingGuns> ChargingGuns { get; set; }
        public virtual DbSet<EVCDTO.EVModelMaster> EVModelMasters { get; set; }
        public virtual DbSet<EVCDTO.PaymentHistory> PaymentHistories { get; set; }
        public virtual DbSet<EVCDTO.UserVehicle> UserVehicles { get; set; }
        public virtual DbSet<EVCDTO.WalletTransactionLog> WalletTransactionLogs { get; set; }
        public virtual DbSet<EVCDTO.FileMaster> FileMasters { get; set; }
        public virtual DbSet<EVCDTO.RefreshToken> RefreshTokens { get; set; }
        public virtual DbSet<EVCDTO.OtpValidation> OtpValidations { get; set; }
        public virtual DbSet<EVCDTO.ChargerTypeMaster> ChargerTypeMasters { get; set; }
        public virtual DbSet<EVCDTO.BatteryTypeMaster> BatteryTypeMasters { get; set; }
        public virtual DbSet<EVCDTO.BatteryCapacityMaster> BatteryCapacityMasters { get; set; }
        public virtual DbSet<EVCDTO.CarManufacturerMaster> CarManufacturerMasters { get; set; }
        public virtual DbSet<EVCDTO.PaymentValidation> PaymentValidations { get; set; }

        // OCPI Tables
        public virtual DbSet<OCPIDTO.OcpiPartnerCredential> OcpiPartnerCredentials { get; set; }
        public virtual DbSet<OCPIDTO.OcpiPartnerLocation> OcpiPartnerLocations { get; set; }
        public virtual DbSet<OCPIDTO.OcpiPartnerEvse> OcpiPartnerEvses { get; set; }
        public virtual DbSet<OCPIDTO.OcpiPartnerConnector> OcpiPartnerConnectors { get; set; }
        public virtual DbSet<OCPIDTO.OcpiPartnerSession> OcpiPartnerSessions { get; set; }
        public virtual DbSet<OCPIDTO.OcpiCdr> OcpiCdrs { get; set; }
        public virtual DbSet<OCPIDTO.OcpiTariff> OcpiTariffs { get; set; }
        public virtual DbSet<OCPIDTO.OcpiToken> OcpiTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ChargePoint>(entity =>
            {
                entity.ToTable("ChargePoint");

                entity.HasIndex(e => e.ChargePointId, "ChargePoint_Identifier")
                    .IsUnique();

                entity.Property(e => e.ChargePointId).HasMaxLength(100);

                entity.Property(e => e.Comment).HasMaxLength(200);

                entity.Property(e => e.Name).HasMaxLength(100);

                entity.Property(e => e.Username).HasMaxLength(50);

                entity.Property(e => e.Password).HasMaxLength(50);

                entity.Property(e => e.ClientCertThumb).HasMaxLength(100);
            });

            modelBuilder.Entity<ChargeTag>(entity =>
            {
                entity.HasKey(e => e.TagId)
                    .HasName("PK_ChargeKeys");

                entity.Property(e => e.TagId).HasMaxLength(50);

                entity.Property(e => e.ParentTagId).HasMaxLength(50);

                entity.Property(e => e.TagName).HasMaxLength(200);
            });

            modelBuilder.Entity<ConnectorStatus>(entity =>
            {
                entity.HasKey(e => new { e.ChargePointId, e.ConnectorId });

                entity.ToTable("ConnectorStatus");

                entity.Property(e => e.ChargePointId).HasMaxLength(100);

                entity.Property(e => e.ConnectorName).HasMaxLength(100);

                entity.Property(e => e.LastStatus).HasMaxLength(100);

                entity.Property(e => e.Active).HasDefaultValue(1);
            });

            modelBuilder.Entity<MessageLog>(entity =>
            {
                entity.HasKey(e => e.LogId);

                entity.ToTable("MessageLog");

                entity.HasIndex(e => e.LogTime, "IX_MessageLog_ChargePointId");

                entity.Property(e => e.ChargePointId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.ErrorCode).HasMaxLength(100);

                entity.Property(e => e.Message)
                    .IsRequired()
                    .HasMaxLength(100);
            });

            modelBuilder.Entity<Transaction>(entity =>
            {
                entity.Property(e => e.Uid).HasMaxLength(50);

                entity.Property(e => e.ChargePointId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.StartTagId).HasMaxLength(50);

                entity.Property(e => e.StartResult).HasMaxLength(100);

                entity.Property(e => e.StopTagId).HasMaxLength(50);

                entity.Property(e => e.StopReason).HasMaxLength(100);

                entity.HasOne(d => d.ChargePoint)
                    .WithMany(p => p.Transactions)
                    .HasForeignKey(d => d.ChargePointId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Transactions_ChargePoint");

                entity.HasIndex(e => new { e.ChargePointId, e.ConnectorId });
            });

            modelBuilder.Entity<EVCDTO.Users>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.FirstName).HasMaxLength(100);

                entity.Property(e => e.LastName).HasMaxLength(100);

                entity.Property(e => e.EMailID).HasMaxLength(200);

                entity.Property(e => e.PhoneNumber).HasMaxLength(20);

                entity.Property(e => e.CountryCode).HasMaxLength(10);

                entity.Property(e => e.Password).HasMaxLength(200);

                entity.Property(e => e.ProfileImageID).HasMaxLength(50);

                entity.Property(e => e.AddressLine1).HasMaxLength(200);

                entity.Property(e => e.AddressLine2).HasMaxLength(200);

                entity.Property(e => e.AddressLine3).HasMaxLength(200);

                entity.Property(e => e.State).HasMaxLength(100);

                entity.Property(e => e.City).HasMaxLength(100);

                entity.Property(e => e.PinCode).HasMaxLength(20);

                entity.Property(e => e.ProfileCompleted).HasMaxLength(10);

                entity.Property(e => e.LastLogin).HasMaxLength(50);

                entity.Property(e => e.UserRole).HasMaxLength(50);

                entity.Property(e => e.CreditBalance).HasMaxLength(50);
            });

            modelBuilder.Entity<EVCDTO.ChargingHub>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.ChargingHubName).HasMaxLength(200);

                entity.Property(e => e.AddressLine1).HasMaxLength(200);

                entity.Property(e => e.AddressLine2).HasMaxLength(200);

                entity.Property(e => e.AddressLine3).HasMaxLength(200);

                entity.Property(e => e.ChargingHubImage).HasMaxLength(50);

                entity.Property(e => e.City).HasMaxLength(100);

                entity.Property(e => e.State).HasMaxLength(100);

                entity.Property(e => e.Pincode).HasMaxLength(20);

                entity.Property(e => e.Latitude).HasMaxLength(50);

                entity.Property(e => e.Longitude).HasMaxLength(50);

                entity.Property(e => e.TypeATariff).HasMaxLength(50);

                entity.Property(e => e.TypeBTariff).HasMaxLength(50);

                entity.Property(e => e.Amenities).HasMaxLength(500);

                entity.Property(e => e.AdditionalInfo1).HasMaxLength(200);

                entity.Property(e => e.AdditionalInfo2).HasMaxLength(200);

                entity.Property(e => e.AdditionalInfo3).HasMaxLength(200);
            });

            modelBuilder.Entity<EVCDTO.ChargingHubReview>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.UserId).HasMaxLength(50);

                entity.Property(e => e.ChargingHubId).HasMaxLength(50);

                entity.Property(e => e.ChargingStationId).HasMaxLength(50);

                entity.Property(e => e.Description).HasMaxLength(1000);

                entity.Property(e => e.ReviewImage1).HasMaxLength(50);

                entity.Property(e => e.ReviewImage2).HasMaxLength(50);

                entity.Property(e => e.ReviewImage3).HasMaxLength(50);

                entity.Property(e => e.ReviewImage4).HasMaxLength(50);

                entity.HasOne<EVCDTO.Users>()
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ChargingHubReview_Users");

                entity.HasOne<EVCDTO.ChargingHub>()
                    .WithMany()
                    .HasForeignKey(d => d.ChargingHubId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ChargingHubReview_ChargingHub");

                entity.HasOne<EVCDTO.ChargingStation>()
                    .WithMany()
                    .HasForeignKey(d => d.ChargingStationId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ChargingHubReview_ChargingStation");
            });

            modelBuilder.Entity<EVCDTO.ChargingSession>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.ChargingGunId).HasMaxLength(50);

                entity.Property(e => e.ChargingStationID).HasMaxLength(50);

                entity.Property(e => e.StartMeterReading).HasMaxLength(50);

                entity.Property(e => e.EndMeterReading).HasMaxLength(50);

                entity.Property(e => e.EnergyTransmitted).HasMaxLength(50);

                entity.Property(e => e.ChargingSpeed).HasMaxLength(50);

                entity.Property(e => e.ChargingTariff).HasMaxLength(50);

                entity.Property(e => e.ChargingTotalFee).HasMaxLength(50);
            });

            modelBuilder.Entity<EVCDTO.ChargingStation>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.ChargingPointId).HasMaxLength(100);

                entity.Property(e => e.ChargingHubId).HasMaxLength(50);

                entity.Property(e => e.ChargingStationImage).HasMaxLength(50);

                entity.HasOne<ChargePoint>()
                    .WithMany()
                    .HasForeignKey(d => d.ChargingPointId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ChargingStation_ChargePoint");

                entity.HasOne<EVCDTO.ChargingHub>()
                    .WithMany()
                    .HasForeignKey(d => d.ChargingHubId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ChargingStation_ChargingHub");
            });

            modelBuilder.Entity<EVCDTO.ChargingGuns>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.ChargingStationId).HasMaxLength(50);

                entity.Property(e => e.ConnectorId).HasMaxLength(50);

                entity.Property(e => e.ChargingHubId).HasMaxLength(50);

                entity.Property(e => e.ChargerTypeId).HasMaxLength(50);

                entity.Property(e => e.ChargerTariff).HasMaxLength(50);

                entity.Property(e => e.PowerOutput).HasMaxLength(50);

                entity.Property(e => e.ChargerStatus).HasMaxLength(50);

                entity.Property(e => e.ChargerMeterReading).HasMaxLength(50);

                entity.Property(e => e.AdditionalInfo1).HasMaxLength(200);

                entity.Property(e => e.AdditionalInfo2).HasMaxLength(200);

                entity.Property(e => e.Active).HasDefaultValue(1);

                entity.HasOne<EVCDTO.ChargingStation>()
                    .WithMany()
                    .HasForeignKey(d => d.ChargingStationId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ChargingGuns_ChargingStation");

                entity.HasOne<EVCDTO.ChargingHub>()
                    .WithMany()
                    .HasForeignKey(d => d.ChargingHubId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ChargingGuns_ChargingHub");

                entity.HasOne<EVCDTO.ChargerTypeMaster>()
                    .WithMany()
                    .HasForeignKey(d => d.ChargerTypeId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_ChargingGuns_ChargerTypeMaster");
            });

            modelBuilder.Entity<EVCDTO.EVModelMaster>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.ModelName).HasMaxLength(100);

                entity.Property(e => e.ManufacturerId).HasMaxLength(50);

                entity.Property(e => e.Variant).HasMaxLength(100);

                entity.Property(e => e.BatterytypeId).HasMaxLength(50);

                entity.Property(e => e.BatteryCapacityId).HasMaxLength(50);

                entity.Property(e => e.CarModelImage).HasMaxLength(50);

                entity.Property(e => e.TypeASupport).HasMaxLength(10);

                entity.Property(e => e.TypeBSupport).HasMaxLength(10);

                entity.Property(e => e.ChadeMOSupport).HasMaxLength(10);

                entity.Property(e => e.CCSSupport).HasMaxLength(10);
            });

            modelBuilder.Entity<EVCDTO.PaymentHistory>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.TransactionType).HasMaxLength(50);

                entity.Property(e => e.UserId).HasMaxLength(50);

                entity.Property(e => e.ChargingStationId).HasMaxLength(50);

                entity.Property(e => e.PaymentMethod).HasMaxLength(50);

                entity.Property(e => e.AdditionalInfo1).HasMaxLength(200);

                entity.Property(e => e.AdditionalInfo2).HasMaxLength(200);

                entity.Property(e => e.AdditionalInfo3).HasMaxLength(200);

                entity.Property(e => e.OrderId).HasMaxLength(100);

                entity.Property(e => e.PaymentId).HasMaxLength(100);

                entity.Property(e => e.UserRemarks).HasMaxLength(500);

                entity.HasOne<EVCDTO.Users>()
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_PaymentHistory_Users");

                entity.HasOne<EVCDTO.ChargingStation>()
                    .WithMany()
                    .HasForeignKey(d => d.ChargingStationId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_PaymentHistory_ChargingStation");
            });

            modelBuilder.Entity<EVCDTO.UserVehicle>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.UserId).HasMaxLength(50);

                entity.Property(e => e.EVManufacturerID).HasMaxLength(50);

                entity.Property(e => e.CarModelID).HasMaxLength(50);

                entity.Property(e => e.CarModelVariant).HasMaxLength(100);

                entity.Property(e => e.CarRegistrationNumber).HasMaxLength(50);

                entity.Property(e => e.BatteryTypeId).HasMaxLength(50);

                entity.Property(e => e.BatteryCapacityId).HasMaxLength(50);

                entity.Property(e => e.ChargerTypeId).HasMaxLength(50);
            });

            modelBuilder.Entity<EVCDTO.WalletTransactionLog>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.UserId).HasMaxLength(50);

                entity.Property(e => e.PreviousCreditBalance).HasMaxLength(50);

                entity.Property(e => e.CurrentCreditBalance).HasMaxLength(50);

                entity.Property(e => e.TransactionType).HasMaxLength(50);

                entity.Property(e => e.PaymentRecId).HasMaxLength(50);

                entity.Property(e => e.ChargingSessionId).HasMaxLength(50);

                entity.Property(e => e.AdditionalInfo1).HasMaxLength(200);

                entity.Property(e => e.AdditionalInfo2).HasMaxLength(200);

                entity.Property(e => e.AdditionalInfo3).HasMaxLength(200);

                entity.HasOne<EVCDTO.Users>()
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_WalletTransactionLog_Users");

                entity.HasOne<EVCDTO.ChargingSession>()
                    .WithMany()
                    .HasForeignKey(d => d.ChargingSessionId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_WalletTransactionLog_ChargingSession");
            });

            modelBuilder.Entity<EVCDTO.FileMaster>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.UserId).HasMaxLength(50);

                entity.Property(e => e.FileName).HasMaxLength(200);

                entity.Property(e => e.FileType).HasMaxLength(50);

                entity.Property(e => e.FileURL).HasMaxLength(500);

                entity.Property(e => e.Remarks).HasMaxLength(500);

                entity.HasOne<EVCDTO.Users>()
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_FileMaster_Users");
            });

            modelBuilder.Entity<EVCDTO.RefreshToken>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.UserId)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Token)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.CreatedByIp).HasMaxLength(50);

                entity.Property(e => e.RevokedByIp).HasMaxLength(50);

                entity.Property(e => e.ReplacedByToken).HasMaxLength(500);

                entity.HasOne<EVCDTO.Users>()
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_RefreshToken_Users");

                entity.HasIndex(e => e.Token);
            });
            modelBuilder.Entity<EVCDTO.RefreshToken>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.UserId)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.Token)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(e => e.CreatedByIp).HasMaxLength(50);

                entity.Property(e => e.RevokedByIp).HasMaxLength(50);

                entity.Property(e => e.ReplacedByToken).HasMaxLength(500);

                entity.HasOne<EVCDTO.Users>()
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_RefreshToken_Users");

                entity.HasIndex(e => e.Token);
            });

            modelBuilder.Entity<EVCDTO.OtpValidation>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.AuthId)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.PhoneNumber)
                    .IsRequired()
                    .HasMaxLength(20);

                entity.Property(e => e.CountryCode)
                    .HasMaxLength(10);

                entity.Property(e => e.OtpCode)
                    .IsRequired()
                    .HasMaxLength(10);

                entity.Property(e => e.Purpose)
                    .HasMaxLength(50);

                entity.Property(e => e.RequestIp).HasMaxLength(50);

                entity.Property(e => e.VerifyIp).HasMaxLength(50);

                entity.Property(e => e.UserId).HasMaxLength(50);

                entity.Property(e => e.IsVerified).HasDefaultValue(false);

                entity.Property(e => e.AttemptCount).HasDefaultValue(0);

                entity.HasIndex(e => e.AuthId);
                entity.HasIndex(e => e.PhoneNumber);
                entity.HasIndex(e => new { e.PhoneNumber, e.CreatedAt });
            });

            modelBuilder.Entity<EVCDTO.ChargerTypeMaster>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.ChargerType)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.ChargerTypeImage).HasMaxLength(50);

                entity.Property(e => e.Additional_Info_1).HasMaxLength(200);

                entity.Property(e => e.Active).HasDefaultValue(1);

                entity.HasIndex(e => e.ChargerType);
            });

            modelBuilder.Entity<EVCDTO.BatteryTypeMaster>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.BatteryType)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.Active).HasDefaultValue(1);

                entity.HasIndex(e => e.BatteryType);
            });

            modelBuilder.Entity<EVCDTO.BatteryCapacityMaster>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.BatteryCapcacity)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.BatteryCapcacityUnit).HasMaxLength(20);

                entity.Property(e => e.Active).HasDefaultValue(1);

                entity.HasIndex(e => e.BatteryCapcacity);
            });

            modelBuilder.Entity<EVCDTO.CarManufacturerMaster>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.ManufacturerName)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.ManufacturerLogoImage).HasMaxLength(50);

                entity.Property(e => e.Active).HasDefaultValue(1);

                entity.HasIndex(e => e.ManufacturerName);
            });

            modelBuilder.Entity<EVCDTO.PaymentValidation>(entity =>
            {
                entity.HasKey(e => e.RecId);

                entity.Property(e => e.RecId).HasMaxLength(50);

                entity.Property(e => e.UserId)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.OrderId)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.Property(e => e.PaymentId).HasMaxLength(100);

                entity.Property(e => e.PaymentSignature).HasMaxLength(500);

                entity.Property(e => e.Currency).HasMaxLength(10);

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.PaymentMethod).HasMaxLength(50);

                entity.Property(e => e.IpAddress).HasMaxLength(50);

                entity.Property(e => e.UserAgent).HasMaxLength(500);

                entity.Property(e => e.PaymentHistoryId).HasMaxLength(50);

                entity.Property(e => e.WalletTransactionId).HasMaxLength(50);

                entity.Property(e => e.VerificationMessage).HasMaxLength(500);

                entity.Property(e => e.SecurityHash).HasMaxLength(500);

                entity.Property(e => e.FailureReason).HasMaxLength(500);

                entity.Property(e => e.Metadata).HasMaxLength(2000);

                entity.Property(e => e.AdditionalInfo1).HasMaxLength(200);

                entity.Property(e => e.AdditionalInfo2).HasMaxLength(200);

                entity.Property(e => e.AdditionalInfo3).HasMaxLength(200);

                entity.Property(e => e.Active).HasDefaultValue(1);

                entity.HasIndex(e => e.OrderId);
                entity.HasIndex(e => e.PaymentId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Status);

                entity.HasOne<EVCDTO.Users>()
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .HasConstraintName("FK_PaymentValidation_Users");
            });

            // OCPI Model Configuration
            modelBuilder.Entity<OCPIDTO.OcpiPartnerCredential>(entity =>
            {
                entity.ToTable("OcpiPartnerCredential");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => new { e.CountryCode, e.PartyId })
                    .IsUnique()
                    .HasDatabaseName("IX_OcpiPartnerCredential_CountryCode_PartyId");
                
                entity.HasIndex(e => e.Token)
                    .IsUnique()
                    .HasDatabaseName("IX_OcpiPartnerCredential_Token");

                entity.Property(e => e.Token).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
                entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
                entity.Property(e => e.PartyId).IsRequired().HasMaxLength(3);
                entity.Property(e => e.BusinessName).HasMaxLength(200);
                entity.Property(e => e.Role).HasMaxLength(50);
                entity.Property(e => e.Version).HasMaxLength(10);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<OCPIDTO.OcpiPartnerLocation>(entity =>
            {
                entity.ToTable("OcpiPartnerLocation");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => new { e.CountryCode, e.PartyId, e.LocationId })
                    .IsUnique()
                    .HasDatabaseName("IX_OcpiPartnerLocation_CountryCode_PartyId_LocationId");

                entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
                entity.Property(e => e.PartyId).IsRequired().HasMaxLength(3);
                entity.Property(e => e.LocationId).IsRequired().HasMaxLength(36);
                entity.Property(e => e.Name).HasMaxLength(255);
                entity.Property(e => e.Address).HasMaxLength(500);
                entity.Property(e => e.City).HasMaxLength(100);
                entity.Property(e => e.PostalCode).HasMaxLength(20);
                entity.Property(e => e.Country).HasMaxLength(3);
                entity.Property(e => e.Latitude).HasMaxLength(20);
                entity.Property(e => e.Longitude).HasMaxLength(20);
                entity.Property(e => e.LocationType).HasMaxLength(50);

                entity.HasOne(d => d.PartnerCredential)
                    .WithMany()
                    .HasForeignKey(d => d.PartnerCredentialId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_OcpiPartnerLocation_PartnerCredential");
            });

            modelBuilder.Entity<OCPIDTO.OcpiPartnerEvse>(entity =>
            {
                entity.ToTable("OcpiPartnerEvse");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => e.EvseUid)
                    .IsUnique()
                    .HasDatabaseName("IX_OcpiPartnerEvse_EvseUid");

                entity.Property(e => e.EvseUid).IsRequired().HasMaxLength(36);
                entity.Property(e => e.EvseId).HasMaxLength(48);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.FloorLevel).HasMaxLength(10);
                entity.Property(e => e.PhysicalReference).HasMaxLength(50);

                entity.HasOne(d => d.PartnerLocation)
                    .WithMany()
                    .HasForeignKey(d => d.PartnerLocationId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_OcpiPartnerEvse_PartnerLocation");
            });

            modelBuilder.Entity<OCPIDTO.OcpiPartnerConnector>(entity =>
            {
                entity.ToTable("OcpiPartnerConnector");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => new { e.PartnerEvseId, e.ConnectorId })
                    .IsUnique()
                    .HasDatabaseName("IX_OcpiPartnerConnector_EvseId_ConnectorId");

                entity.Property(e => e.ConnectorId).IsRequired().HasMaxLength(36);
                entity.Property(e => e.Standard).HasMaxLength(50);
                entity.Property(e => e.Format).HasMaxLength(20);
                entity.Property(e => e.PowerType).HasMaxLength(50);

                entity.HasOne(d => d.PartnerEvse)
                    .WithMany()
                    .HasForeignKey(d => d.PartnerEvseId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_OcpiPartnerConnector_PartnerEvse");
            });

            modelBuilder.Entity<OCPIDTO.OcpiPartnerSession>(entity =>
            {
                entity.ToTable("OcpiPartnerSession");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => new { e.CountryCode, e.PartyId, e.SessionId })
                    .IsUnique()
                    .HasDatabaseName("IX_OcpiPartnerSession_CountryCode_PartyId_SessionId");

                entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
                entity.Property(e => e.PartyId).IsRequired().HasMaxLength(3);
                entity.Property(e => e.SessionId).IsRequired().HasMaxLength(36);
                entity.Property(e => e.Status).HasMaxLength(50);
                entity.Property(e => e.LocationId).HasMaxLength(36);
                entity.Property(e => e.EvseUid).HasMaxLength(36);
                entity.Property(e => e.ConnectorId).HasMaxLength(36);
                entity.Property(e => e.AuthorizationReference).HasMaxLength(36);
                entity.Property(e => e.TokenUid).HasMaxLength(36);
                entity.Property(e => e.Currency).HasMaxLength(3);
                entity.Property(e => e.TotalEnergy).HasColumnType("decimal(18,4)");
                entity.Property(e => e.TotalCost).HasColumnType("decimal(18,2)");

                entity.HasOne(d => d.PartnerCredential)
                    .WithMany()
                    .HasForeignKey(d => d.PartnerCredentialId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_OcpiPartnerSession_PartnerCredential");
            });

            modelBuilder.Entity<OCPIDTO.OcpiCdr>(entity =>
            {
                entity.ToTable("OcpiCdr");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => new { e.CountryCode, e.PartyId, e.CdrId })
                    .IsUnique()
                    .HasDatabaseName("IX_OcpiCdr_CountryCode_PartyId_CdrId");

                entity.HasIndex(e => e.LocalSessionId)
                    .HasDatabaseName("IX_OcpiCdr_LocalSessionId");

                entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
                entity.Property(e => e.PartyId).IsRequired().HasMaxLength(3);
                entity.Property(e => e.CdrId).IsRequired().HasMaxLength(36);
                entity.Property(e => e.SessionId).HasMaxLength(36);
                entity.Property(e => e.AuthorizationReference).HasMaxLength(36);
                entity.Property(e => e.AuthMethod).HasMaxLength(50);
                entity.Property(e => e.LocationId).HasMaxLength(36);
                entity.Property(e => e.EvseUid).HasMaxLength(36);
                entity.Property(e => e.ConnectorId).HasMaxLength(36);
                entity.Property(e => e.MeterId).HasMaxLength(255);
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.TokenUid).HasMaxLength(36);
                entity.Property(e => e.LocalSessionId).HasMaxLength(255);
                entity.Property(e => e.TotalEnergy).HasColumnType("decimal(18,4)");
                entity.Property(e => e.TotalTime).HasColumnType("decimal(18,4)");
                entity.Property(e => e.TotalParkingTime).HasColumnType("decimal(18,4)");
                entity.Property(e => e.TotalCostExclVat).HasColumnType("decimal(18,2)");
                entity.Property(e => e.TotalCostInclVat).HasColumnType("decimal(18,2)");

                entity.HasOne(d => d.PartnerCredential)
                    .WithMany()
                    .HasForeignKey(d => d.PartnerCredentialId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_OcpiCdr_PartnerCredential");
            });

            modelBuilder.Entity<OCPIDTO.OcpiTariff>(entity =>
            {
                entity.ToTable("OcpiTariff");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => new { e.CountryCode, e.PartyId, e.TariffId })
                    .IsUnique()
                    .HasDatabaseName("IX_OcpiTariff_CountryCode_PartyId_TariffId");

                entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
                entity.Property(e => e.PartyId).IsRequired().HasMaxLength(3);
                entity.Property(e => e.TariffId).IsRequired().HasMaxLength(36);
                entity.Property(e => e.Currency).IsRequired().HasMaxLength(3);
                entity.Property(e => e.Type).HasMaxLength(50);
                entity.Property(e => e.EnergyPrice).HasColumnType("decimal(18,4)");
                entity.Property(e => e.TimePrice).HasColumnType("decimal(18,4)");
                entity.Property(e => e.SessionFee).HasColumnType("decimal(18,2)");
                entity.Property(e => e.MinKwh).HasColumnType("decimal(18,4)");
                entity.Property(e => e.MaxKwh).HasColumnType("decimal(18,4)");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            modelBuilder.Entity<OCPIDTO.OcpiToken>(entity =>
            {
                entity.ToTable("OcpiToken");
                entity.HasKey(e => e.Id);
                
                entity.HasIndex(e => new { e.CountryCode, e.PartyId, e.TokenUid })
                    .IsUnique()
                    .HasDatabaseName("IX_OcpiToken_CountryCode_PartyId_TokenUid");

                entity.HasIndex(e => e.TokenUid)
                    .HasDatabaseName("IX_OcpiToken_TokenUid");

                entity.Property(e => e.CountryCode).IsRequired().HasMaxLength(2);
                entity.Property(e => e.PartyId).IsRequired().HasMaxLength(3);
                entity.Property(e => e.TokenUid).IsRequired().HasMaxLength(36);
                entity.Property(e => e.Type).IsRequired().HasMaxLength(50);
                entity.Property(e => e.VisualNumber).HasMaxLength(64);
                entity.Property(e => e.Issuer).HasMaxLength(64);
                entity.Property(e => e.GroupId).HasMaxLength(36);
                entity.Property(e => e.Whitelist).HasMaxLength(50);
                entity.Property(e => e.Language).HasMaxLength(2);
                entity.Property(e => e.DefaultProfileType).HasMaxLength(50);
                entity.Property(e => e.EnergyContract).HasColumnType("decimal(18,4)");
                entity.Property(e => e.Valid).HasDefaultValue(true);

                entity.HasOne(d => d.PartnerCredential)
                    .WithMany()
                    .HasForeignKey(d => d.PartnerCredentialId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_OcpiToken_PartnerCredential");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
