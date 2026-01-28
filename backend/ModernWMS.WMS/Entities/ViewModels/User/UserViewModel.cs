using ModernWMS.Core.Utility;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace ModernWMS.WMS.Entities.ViewModels
{
    public class UserViewModel
    {
        #region constructor

        public UserViewModel()
        {
        }

        #endregion constructor

        #region property

        /// <summary>
        /// primary key
        /// </summary>
        public int id { get; set; } = 0;

        /// <summary>
        /// user's number
        /// </summary>
        [Display(Name = "user_num")]
        [Required(ErrorMessage = "Required")]
        [MaxLength(128, ErrorMessage = "MaxLength")]
        [JsonProperty("user_num")]
        public string UserNum { get; set; } = string.Empty;

        /// <summary>
        /// user's name
        /// </summary>
        [Display(Name = "user_name")]
        [Required(ErrorMessage = "Required")]
        [MaxLength(128, ErrorMessage = "MaxLength")]
        [JsonProperty("user_name")]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// contact
        /// </summary>
        [Display(Name = "contact_tel")]
        [MaxLength(64, ErrorMessage = "MaxLength")]
        [JsonProperty("contact_tel")]
        public string ContactTel { get; set; } = string.Empty;

        /// <summary>
        /// user's role
        /// </summary>
        [Display(Name = "user_role")]
        [Required(ErrorMessage = "Required")]
        [MaxLength(128, ErrorMessage = "MaxLength")]
        [JsonProperty("user_role")]
        public string UserRole { get; set; } = string.Empty;

        /// <summary>
        /// sex
        /// </summary>
        [Display(Name = "sex")]
        [MaxLength(10, ErrorMessage = "MaxLength")]
        [JsonProperty("sex")]
        public string Sex { get; set; } = string.Empty;

        /// <summary>
        /// is_valid
        /// </summary>
        [Display(Name = "is_valid")]
        [JsonProperty("is_valid")]
        public bool IsValid { get; set; } = false;

        /// <summary>
        /// password
        /// </summary>
        [Display(Name = "password")]
        [MaxLength(64, ErrorMessage = "MaxLength")]
        [JsonProperty("auth_string")]
        public string AuthString { get; set; } = string.Empty;

        /// <summary>
        /// creator
        /// </summary>
        [Display(Name = "creator")]
        [MaxLength(64, ErrorMessage = "MaxLength")]
        [JsonProperty("creator")]
        public string Creator { get; set; } = string.Empty;

        /// <summary>
        /// createtime
        /// </summary>
        [Display(Name = "create_time")]
        [DataType(DataType.DateTime, ErrorMessage = "DataType_DateTime")]
        [JsonProperty("create_time")]
        public DateTime CreateTime { get; set; } = UtilConvert.MinDate;

        /// <summary>
        /// last update time
        /// </summary>
        [Display(Name = "last_update_time")]
        [DataType(DataType.DateTime, ErrorMessage = "DataType_DateTime")]
        [JsonProperty("last_update_time")]
        public DateTime LastUpdateTime { get; set; } = UtilConvert.MinDate;

        /// <summary>
        /// tenant
        /// </summary>
        [Display(Name = "tenant")]
        [JsonProperty("tenant_id")]
        public long TenantId { get; set; } = 0;

        #endregion property
    }
}