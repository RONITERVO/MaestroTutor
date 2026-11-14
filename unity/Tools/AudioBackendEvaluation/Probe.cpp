// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Isolated evaluation only. No Unity integration, device access, playback or provider calls.
#include "phonon.h"
#include "phonon_version.h"
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cmath>
#include <cstring>
#include <iostream>
#include <stdexcept>
#include <thread>
#include <vector>

constexpr int Rate = 24000, Frame = 256, IrSamples = 6000, Channels = 4;
constexpr IPLSimulationFlags Flags =
    static_cast<IPLSimulationFlags>(IPL_SIMULATIONFLAGS_DIRECT | IPL_SIMULATIONFLAGS_REFLECTIONS);
std::atomic<int> errors{0};
void IPLCALL Log(IPLLogLevel level, const char *text)
{
    if (level == IPL_LOGLEVEL_ERROR || level == IPL_LOGLEVEL_WARNING)
    {
        ++errors;
        std::cerr << "SDK diagnostic " << static_cast<int>(level) << ": " << text << '\n';
    }
}
void Require(bool condition, const char *why)
{
    if (!condition)
        throw std::runtime_error(why);
}
void Check(IPLerror value, const char *why)
{
    Require(value == IPL_STATUS_SUCCESS, why);
}
IPLCoordinateSpace3 Pose(float x, float y, float z)
{
    return {{1, 0, 0}, {0, 1, 0}, {0, 0, -1}, {x, y, z}};
}
struct ReflectionBuffers
{
    IPLContext context;
    IPLReflectionEffect effect = nullptr;
    IPLAudioBuffer input{}, output{};
    explicit ReflectionBuffers(IPLContext owner) : context(owner) {}
    ReflectionBuffers(const ReflectionBuffers &) = delete;
    ReflectionBuffers &operator=(const ReflectionBuffers &) = delete;
    ~ReflectionBuffers()
    {
        if (effect)
            iplReflectionEffectRelease(&effect);
        if (output.data)
            iplAudioBufferFree(context, &output);
        if (input.data)
            iplAudioBufferFree(context, &input);
    }
};
struct World
{
    World() = default;
    World(const World &) = delete;
    World &operator=(const World &) = delete;
    IPLContext context = nullptr;
    IPLScene scene = nullptr;
    IPLSimulator simulator = nullptr;
    IPLSource sources[2]{};
    IPLStaticMesh room = nullptr, wall = nullptr;
    ~World()
    {
        // All simulation workers have joined before World leaves scope.
        for (auto &source : sources)
            if (source)
            {
                iplSourceRemove(source, simulator);
                iplSourceRelease(&source);
            }
        if (simulator)
        {
            iplSimulatorCommit(simulator);
            iplSimulatorRelease(&simulator);
        }
        for (auto mesh : {room, wall})
            if (mesh)
            {
                iplStaticMeshRemove(mesh, scene);
                iplStaticMeshRelease(&mesh);
            }
        if (scene)
        {
            iplSceneCommit(scene);
            iplSceneRelease(&scene);
        }
        if (context)
            iplContextRelease(&context);
    }
    void Open()
    {
        IPLContextSettings settings{};
        settings.version = STEAMAUDIO_VERSION;
        settings.logCallback = Log;
        settings.simdLevel = IPL_SIMDLEVEL_SSE2;
        settings.flags = IPL_CONTEXTFLAGS_VALIDATION;
        Check(iplContextCreate(&settings, &context), "create context");
        IPLSceneSettings geometry{};
        geometry.type = IPL_SCENETYPE_DEFAULT;
        Check(iplSceneCreate(context, &geometry, &scene), "create scene");
        IPLSimulationSettings simulation{};
        simulation.flags = Flags;
        simulation.sceneType = IPL_SCENETYPE_DEFAULT;
        simulation.reflectionType = IPL_REFLECTIONEFFECTTYPE_CONVOLUTION;
        simulation.maxNumOcclusionSamples = 8;
        simulation.maxNumRays = 1024;
        simulation.numDiffuseSamples = 64;
        simulation.maxDuration = .25f;
        simulation.maxOrder = 1;
        simulation.maxNumSources = 2;
        simulation.numThreads = 1;
        simulation.rayBatchSize = 1;
        simulation.numVisSamples = 8;
        simulation.samplingRate = Rate;
        simulation.frameSize = Frame;
        Check(iplSimulatorCreate(context, &simulation, &simulator), "create simulator");
        iplSimulatorSetScene(simulator, scene);
        IPLSourceSettings sourceSettings{};
        sourceSettings.flags = Flags;
        for (int i = 0; i < 2; i++)
        {
            Check(iplSourceCreate(simulator, &sourceSettings, &sources[i]), "create source");
            iplSourceAdd(sources[i], simulator);
            IPLSimulationInputs inputs{};
            inputs.flags = Flags;
            inputs.directFlags = static_cast<IPLDirectSimulationFlags>(
                IPL_DIRECTSIMULATIONFLAGS_OCCLUSION |
                IPL_DIRECTSIMULATIONFLAGS_DISTANCEATTENUATION);
            inputs.source = Pose(1, 0, i * .4f);
            inputs.occlusionType = IPL_OCCLUSIONTYPE_RAYCAST;
            inputs.numOcclusionSamples = 8;
            inputs.occlusionRadius = .1f;
            inputs.numTransmissionRays = 1;
            inputs.hybridReverbTransitionTime = .125f;
            inputs.hybridReverbOverlapPercent = .25f;
            for (float &value : inputs.reverbScale)
                value = 1;
            iplSourceSetInputs(sources[i], Flags, &inputs);
        }
        IPLSimulationSharedInputs inputs{};
        inputs.listener = Pose(-1, 0, 0);
        inputs.numRays = 1024;
        inputs.numBounces = 8;
        inputs.duration = .25f;
        inputs.order = 1;
        inputs.irradianceMinDistance = .1f;
        iplSimulatorSetSharedInputs(simulator, Flags, &inputs);
        Commit();
    }
    void Commit()
    {
        iplSceneCommit(scene);
        iplSimulatorCommit(simulator);
    }
    IPLStaticMesh Mesh(std::vector<IPLVector3> vertices, std::vector<IPLTriangle> triangles)
    {
        IPLMaterial material{{.2f, .2f, .2f}, .5f, {.01f, .01f, .01f}};
        std::vector<IPLint32> materials(triangles.size(), 0);
        IPLStaticMeshSettings settings{};
        settings.numVertices = static_cast<int>(vertices.size());
        settings.numTriangles = static_cast<int>(triangles.size());
        settings.numMaterials = 1;
        settings.vertices = vertices.data();
        settings.triangles = triangles.data();
        settings.materialIndices = materials.data();
        settings.materials = &material;
        IPLStaticMesh value{};
        Check(iplStaticMeshCreate(scene, &settings, &value), "create geometry");
        iplStaticMeshAdd(value, scene);
        return value;
    }
    void AddRoom()
    {
        room = Mesh({{-3, -3, -3},
                     {3, -3, -3},
                     {3, 3, -3},
                     {-3, 3, -3},
                     {-3, -3, 3},
                     {3, -3, 3},
                     {3, 3, 3},
                     {-3, 3, 3}},
                    {{{0, 2, 1}},
                     {{0, 3, 2}},
                     {{4, 5, 6}},
                     {{4, 6, 7}},
                     {{0, 1, 5}},
                     {{0, 5, 4}},
                     {{3, 7, 6}},
                     {{3, 6, 2}},
                     {{0, 4, 7}},
                     {{0, 7, 3}},
                     {{1, 2, 6}},
                     {{1, 6, 5}}});
        Commit();
    }
    void AddWall(float x)
    {
        wall = Mesh({{x, -2, -2}, {x, 2, -2}, {x, 2, 2}, {x, -2, 2}}, {{{0, 1, 2}}, {{0, 2, 3}}});
        Commit();
    }
    void Remove(IPLStaticMesh &mesh)
    {
        iplStaticMeshRemove(mesh, scene);
        iplStaticMeshRelease(&mesh);
        Commit();
    }
    float Occlusion()
    {
        iplSimulatorRunDirect(simulator);
        IPLSimulationOutputs out{};
        iplSourceGetOutputs(sources[0], IPL_SIMULATIONFLAGS_DIRECT, &out);
        return out.direct.occlusion;
    }
    double Simulate()
    {
        const auto start = std::chrono::steady_clock::now();
        iplSimulatorRunReflections(simulator);
        return std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - start)
            .count();
    }
    double ImpulseEnergy(int index)
    {
        IPLSimulationOutputs results{};
        iplSourceGetOutputs(sources[index], IPL_SIMULATIONFLAGS_REFLECTIONS, &results);
        Require(results.reflections.ir != nullptr, "simulation returned no impulse response");
        IPLAudioSettings audio{Rate, Frame};
        IPLReflectionEffectSettings settings{IPL_REFLECTIONEFFECTTYPE_CONVOLUTION, IrSamples,
                                             Channels};
        ReflectionBuffers buffers(context);
        auto &effect = buffers.effect;
        auto &input = buffers.input;
        auto &output = buffers.output;
        Check(iplReflectionEffectCreate(context, &audio, &settings, &effect),
              "create reflection effect");
        Check(iplAudioBufferAllocate(context, 1, Frame, &input), "allocate input");
        Check(iplAudioBufferAllocate(context, Channels, Frame, &output), "allocate output");
        results.reflections.type = IPL_REFLECTIONEFFECTTYPE_CONVOLUTION;
        results.reflections.numChannels = Channels;
        results.reflections.irSize = IrSamples;
        double energy = 0;
        for (int block = 0; block < IrSamples / Frame + 6; block++)
        {
            std::memset(input.data[0], 0, Frame * sizeof(float));
            if (block == 0)
                input.data[0][0] = 1;
            iplReflectionEffectApply(effect, &results.reflections, &input, &output, nullptr);
            for (int c = 0; c < Channels; c++)
                for (int i = 0; i < Frame; i++)
                {
                    float value = output.data[c][i];
                    Require(std::isfinite(value), "non-finite rendered sample");
                    energy += static_cast<double>(value) * value;
                }
        }
        return energy;
    }
};
int main()
{
    try
    {
        double maximumMs = 0, closed = 0, afterStop = 0, reopened = 0;
        int retiredJobs = 0;
        for (int cycle = 0; cycle < 12; cycle++)
        {
            World world;
            world.Open();
            Require(world.Occlusion() > .99f, "empty scene blocked source");
            world.AddWall(0);
            Require(world.Occlusion() < .01f, "new runtime wall did not obstruct");
            world.Remove(world.wall);
            Require(world.Occlusion() > .99f, "deleted runtime wall still obstructed");
            world.AddWall(3);
            Require(world.Occlusion() > .99f, "moved wall still obstructed old position");
            world.Remove(world.wall);
            world.AddRoom();
            maximumMs = std::max(maximumMs, world.Simulate());
            closed = world.ImpulseEnergy(0);
            Require(closed > 1e-8, "enclosed scene produced no reflected output");
            iplSourceRemove(world.sources[0], world.simulator);
            iplSourceRelease(&world.sources[0]);
            world.Commit();
            maximumMs = std::max(maximumMs, world.Simulate());
            afterStop = world.ImpulseEnergy(1);
            Require(afterStop > 1e-8, "removing one source lost another source's reflections");
            world.Remove(world.room);
            maximumMs = std::max(maximumMs, world.Simulate());
            reopened = world.ImpulseEnergy(1);
            Require(reopened < afterStop * .01, "deleted room retained reflected energy");
            world.AddRoom();
            // The SDK call is synchronous and has no runtime cancellation token.
            // Stop cancels further scheduling; inputs remain owned until join.
            std::atomic<bool> entered{false}, stop{false};
            std::thread job(
                [&]
                {
                    entered = true;
                    do
                    {
                        maximumMs = std::max(maximumMs, world.Simulate());
                    } while (!stop.load());
                });
            while (!entered.load())
                std::this_thread::yield();
            stop = true;
            job.join();
            retiredJobs++;
            Require(world.ImpulseEnergy(1) > 1e-8, "reconstructed room has no reflected output");
            std::cout << "{\"cycle\":" << cycle << ",\"closedEnergy\":" << closed
                      << ",\"remainingSourceEnergy\":" << afterStop
                      << ",\"emptyEnergy\":" << reopened << "}" << std::endl;
        }
        Require(errors.load() == 0, "native validation warnings or errors");
        std::cout
            << "{\"status\":\"passed\",\"sdk\":\"4.8.1\",\"cycles\":12,\"retiredWorkers\":"
            << retiredJobs << ",\"maximumObservedSimulationMs\":" << maximumMs
            << ",\"validationEnabled\":true,\"questAcceptance\":false,\"unityIntegration\":false}"
            << std::endl;
        return 0;
    }
    catch (const std::exception &e)
    {
        std::cerr << e.what() << std::endl;
        return 1;
    }
}
