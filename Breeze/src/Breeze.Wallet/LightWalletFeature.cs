﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Stratis.Bitcoin.Builder.Feature;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using NBitcoin.Protocol;
using Stratis.Bitcoin;
using Stratis.Bitcoin.Builder;
using Stratis.Bitcoin.Connection;
using Stratis.Bitcoin.Consensus;
using Stratis.Bitcoin.Consensus.Deployments;
using Stratis.Bitcoin.Utilities;
using Stratis.Bitcoin.Wallet;
using Stratis.Bitcoin.Wallet.Controllers;

namespace Breeze.Wallet
{
    public class LightWalletFeature : FullNodeFeature
    {
        private readonly IWalletSyncManager walletSyncManager;
        private readonly IWalletManager walletManager;
        private readonly IConnectionManager connectionManager;
        private readonly ConcurrentChain chain;
        private readonly NodeDeployments nodeDeployments;
        private readonly IAsyncLoopFactory asyncLoopFactory;
        private readonly FullNode.CancellationProvider cancellationProvider;

        public LightWalletFeature(IWalletSyncManager walletSyncManager, IWalletManager walletManager, IConnectionManager connectionManager, 
            ConcurrentChain chain, NodeDeployments nodeDeployments, IAsyncLoopFactory asyncLoopFactory, FullNode.CancellationProvider cancellationProvider)
        {
            this.walletSyncManager = walletSyncManager;
            this.walletManager = walletManager;
            this.connectionManager = connectionManager;
            this.chain = chain;
            this.nodeDeployments = nodeDeployments;
            this.asyncLoopFactory = asyncLoopFactory;
            this.cancellationProvider = cancellationProvider;
        }

        public override void Start()
        {
            this.connectionManager.Parameters.TemplateBehaviors.Add(new DropNodesBehaviour(this.chain, this.connectionManager));

            this.walletManager.Initialize();
            this.walletSyncManager.Initialize();

            this.StartDeploymentsChecksLoop();
        }

        public void StartDeploymentsChecksLoop()
        {
            var loopToken = CancellationTokenSource.CreateLinkedTokenSource(this.cancellationProvider.Cancellation.Token);
            this.asyncLoopFactory.Run("LightWalletFeature.CheckDeployments", token =>
                {
                    if(!this.chain.IsDownloaded())
                        return Task.CompletedTask;

                    // check segwit activation on the chain of headers
                    // if segwit is active signal to only connect to 
                    // nodes that also signal they are segwit nodes
                    var flags = this.nodeDeployments.GetFlags(this.walletSyncManager.WalletTip);
                    if (flags.ScriptFlags.HasFlag(ScriptVerify.Witness))
                        this.connectionManager.AddDiscoveredNodesRequirement(NodeServices.NODE_WITNESS);

                    // done checking
                    loopToken.Cancel();
                    
                    return Task.CompletedTask;
                },
                loopToken.Token,
                repeatEvery: TimeSpans.TenSeconds,
                startAfter: TimeSpans.TenSeconds);
        }

        public override void Stop()
        {
            base.Stop();
        }
    }

    public static class LightWalletFeatureExtension
    {
        public static IFullNodeBuilder UseLightWallet(this IFullNodeBuilder fullNodeBuilder)
        {
            fullNodeBuilder.ConfigureFeature(features =>
            {
                features
                    .AddFeature<LightWalletFeature>()
                    .FeatureServices(services =>
                    {
                        services.AddSingleton<IWalletSyncManager, LightWalletSyncManager>();
                        services.AddSingleton<IWalletManager, WalletManager>();
                        services.AddSingleton<IWalletFeePolicy, LightWalletFeePolicy>();
                        services.AddSingleton<WalletController>();

                    });
            });

            return fullNodeBuilder;
        }
    }
}
