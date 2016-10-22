﻿using System.Collections.Generic;

namespace ProtobufGenerator.Configuration
{
    /// <summary>
    /// Represents a processing Job, which defines a directory of proto schema with generation parameters.
    /// </summary>
    public class Job
    {
        /// <summary>
        /// Name of the Job
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The source directory for the proto schema to generate. Also used as an include directory.
        /// </summary>
        public string ProtoDirectory { get; set; }

        /// <summary>
        /// List of include directories where protoc will look to find dependencies. Referenced from Solution Directory
        /// </summary>
        public IEnumerable<string> ImportDirectories { get; set; }

        /// <summary>
        /// List of class names where we want to generate nullable value types, such as int? instead of int
        /// </summary>
        public IEnumerable<string> NullableClasses { get; set; }

        /// <summary>
        /// Dictionary of CustomAnnotations to provide indexed by class name.
        /// </summary>
        public IDictionary<string, IList<string>> CustomAnnotations { get; set; }

        /// <summary>
        /// Output directory for all code elements generated by the Job
        /// </summary>
        public string OutputDirectory { get; set; }

        /// <summary>
        /// Destination namespace for the code elements generated by the Job
        /// </summary>
        public string DestinationNamespace { get; set; }

        /// <summary>
        /// Destination project. Future versions that have an EnvDTE or Roslyn environment available can
        /// add code elements to the destination project.
        /// </summary>
        public string DestinationProject { get; set; }
    }
}