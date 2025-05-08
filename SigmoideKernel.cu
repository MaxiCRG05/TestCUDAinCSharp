extern "C"
__global__ void SigmoideKernel(float* __restrict__ a, float* __restrict__ c, int n) {
    int idx = blockIdx.x * blockDim.x + threadIdx.x;
    if (idx < n) {
        c[idx] = 1.0f / (1.0f + expf(-a[idx]));
    }
}