﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StackExchange.Opserver
{
    public partial class HAProxySettings : Settings<HAProxySettings>, IAfterLoadActions
    {
        public override bool Enabled { get { return Instances.Any() || Groups.Any(); } }

        public ObservableCollection<Group> Groups { get; set; }
        public event EventHandler<Group> GroupAdded = delegate { };
        public event EventHandler<List<Group>> GroupsChanged = delegate { };
        public event EventHandler<Group> GroupRemoved = delegate { };

        public ObservableCollection<Instance> Instances { get; set; }
        public event EventHandler<Instance> InstanceAdded = delegate { };
        public event EventHandler<List<Instance>> InstancesChanged = delegate { };
        public event EventHandler<Instance> InstanceRemoved = delegate { };

        private Dictionary<string, string> _aliases;
        public Dictionary<string, string> Aliases
        {
            get { return _aliases; }
            private set
            {
                _aliases = value;
                AliasesChanged(this, value);
            }
        }
        public event EventHandler<Dictionary<string, string>> AliasesChanged = delegate { };

        public HAProxySettings()
        {
            // Defaults
            QueryTimeoutMs = 60*1000;
            Groups = new ObservableCollection<Group>();
            Instances = new ObservableCollection<Instance>();
            Aliases = new Dictionary<string, string>();
        }

        public void AfterLoad()
        {
            Groups.AddHandlers(this, GroupAdded, GroupsChanged, GroupRemoved);
            Groups.ForEach(g => g.AfterLoad());
            Instances.AddHandlers(this, InstanceAdded, InstancesChanged, InstanceRemoved);
        }

        public InstanceSettings GetInstanceSettings(Instance instance, Group group)
        {
            // Grab setting from node, then category, then global
            Func<Func<IInstanceSettings, string>, string, string> getVal =
                (f, d) => f(instance)
                              .IsNullOrEmptyReturn(group != null ? f(group) : null)
                              .IsNullOrEmptyReturn(d);

            return new InstanceSettings
            {
                Name = instance.Name.IsNullOrEmptyReturn(group != null ? group.Name : "Unknown"),
                Description = instance.Description.IsNullOrEmptyReturn(group != null ? group.Description : "Unknown"),
                QueryTimeoutMs = instance.QueryTimeoutMs ?? (group != null ? group.QueryTimeoutMs : null) ?? QueryTimeoutMs,
                User = getVal(i => i.User, User),
                Password = getVal(i => i.Password, Password),
                AdminUser = getVal(i => i.AdminUser, AdminUser),
                AdminPassword = getVal(i => i.AdminPassword, AdminPassword)
            };
        }

        /// <summary>
        /// Default username to use on all instances
        /// </summary>
        public string User { get; set; }
        /// <summary>
        /// Default password to use on all instances
        /// </summary>
        public string Password { get; set; }


        /// <summary>
        /// Default admin username to use on all instances
        /// </summary>
        public string AdminUser { get; set; }
        /// <summary>
        /// Default admin password to use on all instances
        /// </summary>
        public string AdminPassword { get; set; }

        /// <summary>
        /// Default maximum timeout in milliseconds before giving up on an instance, defaults to 60,000ms
        /// </summary>
        public int QueryTimeoutMs { get; set; }

        public class Group : ISettingsCollectionItem<Group>, IAfterLoadActions, IInstanceSettings
        {
            /// <summary>
            /// Instances in this group
            /// </summary>
            public ObservableCollection<Instance> Instances { get; set; }
            public event EventHandler<Instance> InstanceAdded = delegate { };
            public event EventHandler<List<Instance>> InstancesChanged = delegate { };
            public event EventHandler<Instance> InstanceRemoved = delegate { };

            public Group()
            {
                Instances = new ObservableCollection<Instance>();
            }

            public void AfterLoad()
            {
                Instances.AddHandlers(this, InstanceAdded, InstancesChanged, InstanceRemoved);
            }
            
            /// <summary>
            /// The name that appears for this group
            /// </summary>
            public string Name { get; set; }

            public string Description { get; set; }

            /// <summary>
            /// Maximum timeout in milliseconds before giving up on this instances in this group
            /// </summary>
            public int? QueryTimeoutMs { get; set; }

            /// <summary>
            /// Username to use for this group of instances, unless specified by the individual instance
            /// </summary>
            public string User { get; set; }

            /// <summary>
            /// Password to use for this group of instances, unless specified by the individual instance
            /// </summary>
            public string Password { get; set; }

            /// <summary>
            /// Admin Username to use for this group of instances, unless specified by the individual instance
            /// </summary>
            public string AdminUser { get; set; }

            /// <summary>
            /// Admin Default admin password to use on all group of instances, unless specified by the individual instance
            /// </summary>
            public string AdminPassword { get; set; }

            public bool Equals(Group other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Instances.SequenceEqual(other.Instances)
                    && QueryTimeoutMs == other.QueryTimeoutMs
                    && string.Equals(Name, other.Name) 
                    && string.Equals(Description, other.Description);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Group) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = 0;
                    foreach (var i in Instances)
                        hashCode = (hashCode * 397) ^ i.GetHashCode();
                    hashCode = (hashCode*397) ^ QueryTimeoutMs.GetHashCode();
                    hashCode = (hashCode*397) ^ (Name != null ? Name.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ (Description != null ? Description.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        public class Instance : ISettingsCollectionItem<Instance>, IInstanceSettings
        {
            /// <summary>
            /// URL to use for this instance
            /// </summary>
            public string Url { get; set; }

            /// <summary>
            /// The name that appears for this instance
            /// </summary>
            public string Name { get; set; }

            public string Description { get; set; }

            /// <summary>
            /// Maximum timeout in milliseconds before giving up on this instance
            /// </summary>
            public int? QueryTimeoutMs { get; set; }

            /// <summary>
            /// Username to use for this instance
            /// </summary>
            public string User { get; set; }

            /// <summary>
            /// Password to use for this instance
            /// </summary>
            public string Password { get; set; }

            /// <summary>
            /// Admin Username to use for this instance
            /// </summary>
            public string AdminUser { get; set; }

            /// <summary>
            /// Admin Default admin password to use on all instances
            /// </summary>
            public string AdminPassword { get; set; }

            public bool Equals(Instance other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return string.Equals(Url, other.Url)
                       && QueryTimeoutMs == other.QueryTimeoutMs
                       && string.Equals(User, other.User)
                       && string.Equals(Password, other.Password)
                       && string.Equals(AdminUser, other.AdminUser)
                       && string.Equals(AdminPassword, other.AdminPassword);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Instance) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (Url != null ? Url.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ QueryTimeoutMs.GetHashCode();
                    hashCode = (hashCode*397) ^ (User != null ? User.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ (Password != null ? Password.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ (AdminUser != null ? AdminUser.GetHashCode() : 0);
                    hashCode = (hashCode*397) ^ (AdminPassword != null ? AdminPassword.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }

        public class InstanceSettings
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public int? QueryTimeoutMs { get; set; }
            public string User { get; set; }
            public string Password { get; set; }
            public string AdminUser { get; set; }
            public string AdminPassword { get; set; }
        }

        public interface IInstanceSettings
        {
            string Name { get; set; }
            string Description { get; set; }
            int? QueryTimeoutMs { get; set; }
            string User { get; set; }
            string Password { get; set; }
            string AdminUser { get; set; }
            string AdminPassword { get; set; }
        }
    }
}
