# appsettings.POC.json Configuration Notes

## POC OPTIMIZATION: Smaller Embedding Dimensions

**Setting:** `"EmbeddingDimension": 256`

### Why 256d instead of 768d?

EmbeddingGemma supports **Matryoshka Representation Learning (MRL)**, which allows you to truncate the output embedding and get:

| Dimension | Speed Gain | Accuracy | Storage | Use Case |
|-----------|------------|----------|---------|----------|
| **768d** (full) | 1x baseline | 100% | 3 KB/embedding | Production |
| **512d** | 1.5x faster | ~99% | 2 KB/embedding | Balanced |
| **256d** | 3x faster | ~95% | 1 KB/embedding | **POC (Recommended)** |
| **128d** | 6x faster | ~90% | 512 bytes/embedding | Fast prototyping |

### Benefits for POC:
- ✅ **3x faster inference** (256d vs 768d)
- ✅ **3x less storage** (important for large datasets)
- ✅ **Lower memory usage** (runs on smaller instances)
- ✅ **Still good accuracy** (~95% of full model)

### Migration Strategy:
```
POC Phase:     256d (fast, cheap, good enough)
Production:    768d (when you fine-tune on real data)
```

### How to Change:

**For even faster POC (if 256d is still slow):**
```json
"EmbeddingDimension": 128
```

**For production (after fine-tuning):**
```json
"EmbeddingDimension": 768
```

**Environment variable override:**
```bash
export Embedding__EmbeddingDimension=256
```

## Current Model: onnx-community/embeddinggemma-300m-ONNX

**Specs:**
- **Size:** 197 MB (q4 quantized)
- **Cost:** $0 (free, self-hosted)
- **Hardware:** CPU-only (no GPU needed)
- **Speed:** ~10-50ms per embedding (256d on 4-core CPU)
- **Memory:** ~2-4 GB RAM

**Perfect for:**
- Resource-constrained POC
- Budget-limited projects
- CPU-only infrastructure
- Fast prototyping

## AutoDownload Enabled

```json
"AutoDownload": true
```

- First run will download model automatically (takes 1-2 minutes)
- Subsequent runs use cached model
- No manual download needed

## Next Steps

1. **Start POC:** `docker-compose up -d`
2. **Test speed:** Benchmark with 256d
3. **If too slow:** Try 128d
4. **If too inaccurate:** Use 512d or 768d
5. **Monitor:** Log query times and user feedback

## Related Files

- `appsettings.json` - Production config (768d, AutoDownload=false)
- `appsettings.Development.json` - Dev config (768d, local paths)
- `appsettings.POC.json` - **This file** (256d, optimized for POC)
- `POC_TO_PRODUCTION.md` - Migration guide
