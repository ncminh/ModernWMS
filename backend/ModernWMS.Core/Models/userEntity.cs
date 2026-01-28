using Microsoft.AspNetCore.Mvc.RazorPages;
using ModernWMS.Core.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModernWMS.Core.Models
{
    /// <summary>
    /// user entity
    /// </summary>
    [Table("user")]
    public class UserEntity : BaseModel
    {

        #region property

        /// <summary>
        /// user's number
        /// </summary>
        [Column("user_num")]
        public string UserNum { get; set; } = string.Empty;

        /// <summary>
        /// user's name
        /// </summary>
        [Column("user_name")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// contact
        /// </summary>
        [Column("contact_tel")]
        public string ContactTel { get; set; } = string.Empty;

        /// <summary>
        /// user's role
        /// </summary>
        [Column("user_role")]
        public string UserRole { get; set; } = string.Empty;

        /// <summary>
        /// sex
        /// </summary>
        [Column("sex")]
        public string Sex { get; set; } = string.Empty;

        /// <summary>
        /// is_valid
        /// </summary>
        [Column("is_valid")]
        public bool IsValid { get; set; } = false;

        /// <summary>
        /// password
        /// </summary>
        [Column("auth_string")]
        public string AuthString { get; set; } = string.Empty;

        /// <summary>
        /// email
        /// </summary>
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// creator
        /// </summary>
        [Column("creator")]
        public string Creator { get; set; } = string.Empty;

        /// <summary>
        /// createtime
        /// </summary>
        [Column("create_time")]
        public DateTime CreateTime { get; set; } = UtilConvert.MinDate;

        /// <summary>
        /// last update time
        /// </summary>
        [Column("last_update_time")]
        public DateTime LastUpdateTime { get; set; } = UtilConvert.MinDate;

        /// <summary>
        /// tenant
        /// </summary>
        [Column("tenant_id")]
        public long TenantId { get; set; } = 0;

        #endregion
    }
}
