﻿using ProtobufGenerator.Interfaces;
using System;
using System.Collections.Generic;
using ProtobufGenerator.Generation;
using ProtobufGenerator.Extensions;

namespace ProtobufGenerator
{
    public class ProtoEngine : IGenerateProto
    {
        private readonly IConfiguration _config;
        private readonly Dictionary<string, JobResult> _jobResults = new Dictionary<string, JobResult>();

        public ProtoEngine(IConfiguration configuration)
        {
            _config = Check.NotNull(configuration, nameof(configuration));
        }

        public void ProcessProto()
        {
            throw new NotImplementedException();
        }
    }
}
