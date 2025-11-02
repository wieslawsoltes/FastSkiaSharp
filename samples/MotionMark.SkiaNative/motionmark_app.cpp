#include "include/core/SkCanvas.h"
#include "include/core/SkColor.h"
#include "include/core/SkPaint.h"
#include "include/core/SkPathBuilder.h"
#include "include/core/SkPoint.h"
#include "include/core/SkSurface.h"
#include "tools/sk_app/Application.h"
#include "tools/sk_app/Window.h"
#include "tools/window/DisplayParams.h"

#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <memory>
#include <random>
#include <utility>
#include <vector>

namespace {

using Clock = std::chrono::steady_clock;

constexpr int kGridWidth = 80;
constexpr int kGridHeight = 40;

constexpr std::array<SkColor, 7> kPalette = {
        SkColorSetARGB(0xFF, 0x10, 0x10, 0x10),
        SkColorSetARGB(0xFF, 0x80, 0x80, 0x80),
        SkColorSetARGB(0xFF, 0xC0, 0xC0, 0xC0),
        SkColorSetARGB(0xFF, 0x10, 0x10, 0x10),
        SkColorSetARGB(0xFF, 0x80, 0x80, 0x80),
        SkColorSetARGB(0xFF, 0xC0, 0xC0, 0xC0),
        SkColorSetARGB(0xFF, 0xE0, 0x10, 0x40),
};

constexpr std::array<std::pair<int, int>, 4> kOffsets = {{
        {-4, 0},
        {2, 0},
        {1, -2},
        {1, 2},
}};

struct GridPoint {
    int x = 0;
    int y = 0;
};

enum class SegmentKind : uint8_t {
    kLine,
    kQuad,
    kCubic,
};

struct Element {
    SegmentKind kind = SegmentKind::kLine;
    GridPoint start;
    GridPoint control1;
    GridPoint control2;
    GridPoint end;
    SkColor color = SK_ColorWHITE;
    float width = 1.0f;
    bool split = false;
};

class MotionMarkLayer final : public sk_app::Window::Layer {
public:
    MotionMarkLayer()
        : fRng(static_cast<uint32_t>(Clock::now().time_since_epoch().count())) {
        fStrokePaint.setAntiAlias(true);
        fStrokePaint.setStyle(SkPaint::kStroke_Style);
        fStrokePaint.setStrokeCap(SkPaint::kRound_Cap);
        fStrokePaint.setStrokeJoin(SkPaint::kRound_Join);

        fBackgroundPaint.setStyle(SkPaint::kFill_Style);
        fBackgroundPaint.setColor(SkColorSetRGB(12, 16, 24));

        fElements.reserve(this->computeElementCount(fComplexity));
        this->resizeElements(this->computeElementCount(fComplexity));
    }

    void onResize(int width, int height) override {
        fWidth = std::max(1, width);
        fHeight = std::max(1, height);
    }

    void onPaint(SkSurface* surface) override {
        SkCanvas* canvas = surface->getCanvas();
        canvas->clear(fBackgroundPaint.getColor());

        if (fElements.empty()) {
            return;
        }

        const float scaleX = static_cast<float>(fWidth) / static_cast<float>(kGridWidth + 1);
        const float scaleY = static_cast<float>(fHeight) / static_cast<float>(kGridHeight + 1);
        const float uniformScale = std::max(0.0f, std::min(scaleX, scaleY));
        if (uniformScale <= 0.f) {
            return;
        }

        const float offsetX = (static_cast<float>(fWidth) - uniformScale * (kGridWidth + 1)) * 0.5f;
        const float offsetY = (static_cast<float>(fHeight) - uniformScale * (kGridHeight + 1)) * 0.5f;

        SkPathBuilder pathBuilder;
        bool pathStarted = false;

        for (size_t i = 0; i < fElements.size(); ++i) {
            Element& element = fElements[i];

            if (!pathStarted) {
                const SkPoint start = this->toPoint(element.start, uniformScale, offsetX, offsetY);
                pathBuilder.moveTo(start);
                pathStarted = true;
            }

            switch (element.kind) {
                case SegmentKind::kLine: {
                    const SkPoint end = this->toPoint(element.end, uniformScale, offsetX, offsetY);
                    pathBuilder.lineTo(end);
                    break;
                }
                case SegmentKind::kQuad: {
                    const SkPoint c1 = this->toPoint(element.control1, uniformScale, offsetX, offsetY);
                    const SkPoint end = this->toPoint(element.end, uniformScale, offsetX, offsetY);
                    pathBuilder.quadTo(c1, end);
                    break;
                }
                case SegmentKind::kCubic: {
                    const SkPoint c1 = this->toPoint(element.control1, uniformScale, offsetX, offsetY);
                    const SkPoint c2 = this->toPoint(element.control2, uniformScale, offsetX, offsetY);
                    const SkPoint end = this->toPoint(element.end, uniformScale, offsetX, offsetY);
                    pathBuilder.cubicTo(c1, c2, end);
                    break;
                }
            }

            const bool finalize = element.split || i + 1 == fElements.size();
            if (finalize && !pathBuilder.isEmpty()) {
                fStrokePaint.setColor(element.color);
                fStrokePaint.setStrokeWidth(element.width);
                canvas->drawPath(pathBuilder.detach(), fStrokePaint);
                pathStarted = false;
            }

            if (fUnitDist(fRng) > 0.995f) {
                element.split = !element.split;
            }
        }
    }

    void setComplexity(int complexity) {
        complexity = std::clamp(complexity, 0, 24);
        if (complexity == fComplexity) {
            return;
        }
        fComplexity = complexity;
        this->resizeElements(this->computeElementCount(fComplexity));
    }

    int complexity() const { return fComplexity; }
    size_t elementCount() const { return fElements.size(); }

private:
    static int computeElementCount(int complexity) {
        if (complexity < 10) {
            return (complexity + 1) * 1000;
        }
        const int extended = (complexity - 8) * 10000;
        return std::min(extended, 120000);
    }

    void resizeElements(int targetCount) {
        const int current = static_cast<int>(fElements.size());
        if (targetCount == current) {
            return;
        }

        if (targetCount < current) {
            fElements.resize(targetCount);
            fLastGridPoint = (targetCount > 0) ? fElements.back().end
                                               : GridPoint{kGridWidth / 2, kGridHeight / 2};
            return;
        }

        fElements.reserve(targetCount);
        if (current == 0) {
            fLastGridPoint = GridPoint{kGridWidth / 2, kGridHeight / 2};
        } else {
            fLastGridPoint = fElements.back().end;
        }

        for (int i = current; i < targetCount; ++i) {
            Element element = this->createRandomElement(fLastGridPoint);
            fElements.push_back(element);
            fLastGridPoint = element.end;
        }
    }

    Element createRandomElement(const GridPoint& last) {
        std::uniform_int_distribution<int> segmentDist(0, 3);
        std::uniform_int_distribution<size_t> paletteDist(0, kPalette.size() - 1);
        std::bernoulli_distribution splitDist(0.5);

        const int segType = segmentDist(fRng);
        const GridPoint next = this->randomPoint(last);

        Element element;
        element.start = last;

        if (segType < 2) {
            element.kind = SegmentKind::kLine;
            element.end = next;
        } else if (segType == 2) {
            const GridPoint p2 = this->randomPoint(next);
            element.kind = SegmentKind::kQuad;
            element.control1 = next;
            element.end = p2;
        } else {
            const GridPoint p2 = this->randomPoint(next);
            const GridPoint p3 = this->randomPoint(next);
            element.kind = SegmentKind::kCubic;
            element.control1 = next;
            element.control2 = p2;
            element.end = p3;
        }

        element.color = kPalette[paletteDist(fRng)];
        const double widthFactor = std::pow(static_cast<double>(fUnitDist(fRng)), 5.0);
        element.width = static_cast<float>(widthFactor * 20.0 + 1.0);
        element.split = splitDist(fRng);
        return element;
    }

    GridPoint randomPoint(const GridPoint& last) {
        std::uniform_int_distribution<size_t> offsetDist(0, kOffsets.size() - 1);
        const auto [dx, dy] = kOffsets[offsetDist(fRng)];

        int x = last.x + dx;
        if (x < 0 || x > kGridWidth) {
            x -= dx * 2;
        }

        int y = last.y + dy;
        if (y < 0 || y > kGridHeight) {
            y -= dy * 2;
        }

        return GridPoint{x, y};
    }

    static SkPoint toPoint(const GridPoint& pt, float scale, float offsetX, float offsetY) {
        const float px = offsetX + (static_cast<float>(pt.x) + 0.5f) * scale;
        const float py = offsetY + (static_cast<float>(pt.y) + 0.5f) * scale;
        return SkPoint::Make(px, py);
    }

    std::vector<Element> fElements;
    GridPoint fLastGridPoint{kGridWidth / 2, kGridHeight / 2};
    SkPaint fStrokePaint;
    SkPaint fBackgroundPaint;
    std::mt19937 fRng;
    std::uniform_real_distribution<float> fUnitDist{0.0f, 1.0f};
    int fComplexity = 8;
    int fWidth = 1280;
    int fHeight = 720;
};

class MotionMarkApplication final : public sk_app::Application {
public:
    MotionMarkApplication(std::unique_ptr<sk_app::Window> window, int complexityArg)
        : fWindow(std::move(window))
        , fRequestedComplexity(complexityArg) {}

    bool init() {
        if (!fWindow) {
            return false;
        }

        bool attached = false;

#if defined(SK_GRAPHITE)
    #if defined(SK_METAL)
        attached = fWindow->attach(sk_app::Window::kGraphiteMetal_BackendType);
    #elif defined(SK_DAWN)
        attached = fWindow->attach(sk_app::Window::kGraphiteDawn_BackendType);
    #elif defined(SK_VULKAN)
        attached = fWindow->attach(sk_app::Window::kGraphiteVulkan_BackendType);
    #endif
#endif

#if defined(SK_METAL)
        if (!attached) {
            attached = fWindow->attach(sk_app::Window::kMetal_BackendType);
        }
#endif

#if defined(SK_GL)
        if (!attached) {
            attached = fWindow->attach(sk_app::Window::kNativeGL_BackendType);
        }
#endif

        if (!attached) {
            attached = fWindow->attach(sk_app::Window::kRaster_BackendType);
        }

        if (!attached) {
            return false;
        }

        skwindow::DisplayParamsBuilder paramsBuilder(fWindow->getRequestedDisplayParams());
        paramsBuilder.msaaSampleCount(4);
        fWindow->setRequestedDisplayParams(paramsBuilder.detach());

        fLayer = std::make_unique<MotionMarkLayer>();
        if (fRequestedComplexity >= 0) {
            fLayer->setComplexity(fRequestedComplexity);
        }

        fLayer->onResize(fWindow->width(), fWindow->height());
        fWindow->pushLayer(fLayer.get());
        fWindow->setTitle("MotionMark Native (Skia)");
        fWindow->show();
        fWindow->inval();

        fLastTick = Clock::now();
        return true;
    }

    void onIdle() override {
        const auto now = Clock::now();
        const double dt = std::clamp(
                std::chrono::duration<double>(now - fLastTick).count(), 1.0 / 240.0, 0.25);
        fLastTick = now;

        fWindow->inval();

        fAccumulatedTime += dt;
        ++fFrameCounter;

        if (fAccumulatedTime >= 0.5 && fLayer) {
            const double fps = static_cast<double>(fFrameCounter) / fAccumulatedTime;
            char title[160];
            std::snprintf(title,
                          sizeof(title),
                          "MotionMark Native (Skia)  |  %.1f FPS  |  Complexity %d  |  Elements %zu",
                          fps,
                          fLayer->complexity(),
                          fLayer->elementCount());
            fWindow->setTitle(title);
            fAccumulatedTime = 0.0;
            fFrameCounter = 0;
        }
    }

private:
    std::unique_ptr<sk_app::Window> fWindow;
    std::unique_ptr<MotionMarkLayer> fLayer;
    Clock::time_point fLastTick = Clock::now();
    double fAccumulatedTime = 0.0;
    int fFrameCounter = 0;
    int fRequestedComplexity = -1;
};

int parseComplexityArg(int argc, char** argv) {
    constexpr char kPrefix[] = "--complexity=";
    const size_t prefixLen = sizeof(kPrefix) - 1;
    for (int i = 1; i < argc; ++i) {
        if (std::strncmp(argv[i], kPrefix, prefixLen) == 0) {
            return std::atoi(argv[i] + prefixLen);
        }
    }
    return -1;
}

}  // namespace

sk_app::Application* sk_app::Application::Create(int argc, char** argv, void* platformData) {
    std::unique_ptr<sk_app::Window> window(sk_app::Windows::CreateNativeWindow(platformData));
    if (!window) {
        return nullptr;
    }

    const int complexityArg = parseComplexityArg(argc, argv);
    auto app = std::make_unique<MotionMarkApplication>(std::move(window), complexityArg);
    if (!app->init()) {
        return nullptr;
    }
    return app.release();
}
