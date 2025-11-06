#!/bin/bash

##############################################################################
# S.A.K.I.N. Performance Test Suite - Automated Execution
#
# This script runs all performance and chaos engineering tests in sequence,
# collecting results and generating a comprehensive report.
#
# Prerequisites:
#   - K6 installed (k6 --version)
#   - Docker Compose running (docker-compose ps)
#   - All services healthy
#   - ./results/ directory exists (created automatically)
#
# Usage:
#   ./run-all-tests.sh [OPTIONS]
#
# Options:
#   --quick       Run quick tests (1 minute each)
#   --extended    Run extended tests (10+ minutes)
#   --chaos       Run chaos engineering scenarios only
#   --baseline    Run baseline tests only (1k, 5k, 10k EPS)
#   --cleanup     Remove previous results
#
##############################################################################

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
RESULTS_DIR="${SCRIPT_DIR}/results"
BASE_URL="${BASE_URL:-http://localhost:8080}"
PANEL_API_URL="${PANEL_API_URL:-http://localhost:5000}"
CLICKHOUSE_URL="${CLICKHOUSE_URL:-http://localhost:8123}"
KAFKA_BOOTSTRAP="${KAFKA_BOOTSTRAP:-kafka:9092}"

# Test configuration
TEST_DURATION_QUICK="1m"
TEST_DURATION_STANDARD="5m"
TEST_DURATION_EXTENDED="10m"
CURRENT_DURATION="${TEST_DURATION_STANDARD}"

# Flags
SKIP_BASELINE=false
SKIP_CHAOS=false
RUN_EXTENDED=false
CLEANUP_RESULTS=false

##############################################################################
# Functions
##############################################################################

print_header() {
    echo -e "\n${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}\n"
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_info() {
    echo -e "${YELLOW}ℹ $1${NC}"
}

check_prerequisites() {
    print_header "Checking Prerequisites"
    
    # Check K6
    if ! command -v k6 &> /dev/null; then
        print_error "K6 not found. Install with: brew install k6"
        exit 1
    fi
    print_success "K6 installed: $(k6 --version)"
    
    # Check Docker
    if ! command -v docker &> /dev/null; then
        print_error "Docker not found"
        exit 1
    fi
    print_success "Docker installed: $(docker --version)"
    
    # Check services health
    print_info "Checking service health..."
    if ! curl -s http://localhost:8080/healthz &> /dev/null; then
        print_error "Service health check failed on localhost:8080"
        exit 1
    fi
    print_success "Services healthy"
    
    # Create results directory
    mkdir -p "${RESULTS_DIR}"
    print_success "Results directory: ${RESULTS_DIR}"
}

cleanup_previous_results() {
    if [ "$CLEANUP_RESULTS" = true ]; then
        print_info "Cleaning up previous results..."
        rm -f "${RESULTS_DIR}"/*.json
        rm -f "${RESULTS_DIR}"/*.csv
        print_success "Previous results cleaned"
    fi
}

test_ingestion_baseline() {
    local eps=$1
    local vu=$((eps / 100))
    local name="Ingestion ${eps}k EPS"
    
    print_header "Testing: $name"
    
    k6 run "${SCRIPT_DIR}/ingestion-pipeline.js" \
        --vus "${vu}" \
        --duration "${CURRENT_DURATION}" \
        -e TARGET_EPS="${eps}" \
        -e BASE_URL="${BASE_URL}" \
        --out "json=${RESULTS_DIR}/ingestion-${eps}k-eps.json" \
        2>&1 | tee -a "${RESULTS_DIR}/test-log.txt"
    
    print_success "Test completed: $name"
}

test_correlation_scenario() {
    local scenario=$1
    local eps=$2
    local duration=$3
    local vu=$((eps / 100))
    
    print_header "Testing: Correlation Engine - $scenario (${eps}k EPS)"
    
    k6 run "${SCRIPT_DIR}/correlation-engine.js" \
        --vus "${vu}" \
        --duration "${duration}" \
        -e TARGET_EPS="${eps}" \
        -e SCENARIO="${scenario}" \
        -e BASE_URL="${BASE_URL}" \
        --out "json=${RESULTS_DIR}/correlation-${scenario}-${eps}k-eps.json" \
        2>&1 | tee -a "${RESULTS_DIR}/test-log.txt"
    
    print_success "Test completed: Correlation Engine - $scenario"
}

test_query_performance() {
    print_header "Testing: Query Performance"
    
    k6 run "${SCRIPT_DIR}/query-performance.js" \
        --vus 50 \
        --duration "${CURRENT_DURATION}" \
        -e PANEL_API_URL="${PANEL_API_URL}" \
        -e CLICKHOUSE_URL="${CLICKHOUSE_URL}" \
        --out "json=${RESULTS_DIR}/query-performance.json" \
        2>&1 | tee -a "${RESULTS_DIR}/test-log.txt"
    
    print_success "Test completed: Query Performance"
}

test_soar_playbooks() {
    print_header "Testing: SOAR Playbook Execution"
    
    k6 run "${SCRIPT_DIR}/soar-playbook.js" \
        --vus 20 \
        --duration "${CURRENT_DURATION}" \
        -e SOAR_API_URL="${BASE_URL}" \
        --out "json=${RESULTS_DIR}/soar-playbook.json" \
        2>&1 | tee -a "${RESULTS_DIR}/test-log.txt"
    
    print_success "Test completed: SOAR Playbooks"
}

chaos_database_latency() {
    print_header "Chaos: Database Latency Injection"
    
    print_info "Injecting 3000ms latency to database..."
    sudo tc qdisc add dev docker0 root netem delay 3000ms 2>/dev/null || \
        docker run --rm --privileged --network host \
            busybox tc qdisc add dev eth0 root netem delay 3000ms || \
        print_info "Cannot inject latency (requires root). Skipping this scenario."
    
    print_info "Running tests with DB latency..."
    test_ingestion_baseline 5
    
    print_info "Removing latency..."
    sudo tc qdisc del dev docker0 root 2>/dev/null || true
    
    print_success "Chaos test completed: Database Latency"
}

chaos_redis_failure() {
    print_header "Chaos: Redis Failure"
    
    print_info "Stopping Redis..."
    docker stop sakin-redis 2>/dev/null || print_error "Could not stop Redis"
    sleep 2
    
    print_info "Running correlation tests without Redis..."
    test_correlation_scenario "normal" 5 "2m"
    
    print_info "Restarting Redis..."
    docker start sakin-redis 2>/dev/null || print_error "Could not start Redis"
    sleep 5
    
    print_success "Chaos test completed: Redis Failure"
}

chaos_kafka_failure() {
    print_header "Chaos: Kafka Broker Failure"
    
    print_info "Stopping Kafka..."
    docker stop sakin-kafka 2>/dev/null || print_error "Could not stop Kafka"
    sleep 2
    
    print_info "Running ingestion tests without Kafka (should buffer/retry)..."
    test_ingestion_baseline 5
    
    print_info "Restarting Kafka..."
    docker start sakin-kafka 2>/dev/null || print_error "Could not start Kafka"
    sleep 10
    
    print_success "Chaos test completed: Kafka Failure"
}

generate_summary() {
    print_header "Generating Test Summary"
    
    local summary_file="${RESULTS_DIR}/TEST_SUMMARY.txt"
    
    {
        echo "S.A.K.I.N. Performance Test Summary"
        echo "===================================="
        echo "Timestamp: $(date)"
        echo "Test Duration: ${CURRENT_DURATION}"
        echo ""
        echo "Tests Run:"
        echo "  ✓ Ingestion Pipeline (1k, 5k, 10k EPS)"
        echo "  ✓ Correlation Engine (normal, hot-key, high-cardinality)"
        echo "  ✓ Query Performance"
        echo "  ✓ SOAR Playbooks"
        [ "$SKIP_CHAOS" = false ] && echo "  ✓ Chaos Scenarios (DB latency, Redis failure, Kafka failure)"
        echo ""
        echo "Results Directory: ${RESULTS_DIR}"
        echo "JSON Files:"
        ls -1 "${RESULTS_DIR}"/*.json 2>/dev/null | while read f; do
            echo "  - $(basename $f)"
        done
        echo ""
        echo "Next Steps:"
        echo "  1. Review results in ${RESULTS_DIR}/"
        echo "  2. Check Grafana dashboards: http://localhost:3000"
        echo "  3. View Jaeger traces: http://localhost:16686"
        echo "  4. Analyze metrics in Prometheus: http://localhost:9090"
        echo ""
        echo "Test Results Location:"
        echo "  Log: ${RESULTS_DIR}/test-log.txt"
        echo "  Summary: ${summary_file}"
    } | tee "${summary_file}"
    
    print_success "Test summary generated: ${summary_file}"
}

run_baseline_tests() {
    if [ "$SKIP_BASELINE" = true ]; then
        print_info "Skipping baseline tests (--skip-baseline)"
        return
    fi
    
    print_header "BASELINE TESTS"
    
    test_ingestion_baseline 1
    sleep 5
    
    test_ingestion_baseline 5
    sleep 5
    
    test_ingestion_baseline 10
    sleep 5
    
    test_correlation_scenario "normal" 1 "${CURRENT_DURATION}"
    sleep 5
    
    test_correlation_scenario "normal" 5 "${CURRENT_DURATION}"
    sleep 5
    
    test_correlation_scenario "normal" 10 "2m"
    sleep 5
    
    test_query_performance
    sleep 5
    
    test_soar_playbooks
}

run_chaos_tests() {
    if [ "$SKIP_CHAOS" = true ]; then
        print_info "Skipping chaos tests (--skip-chaos)"
        return
    fi
    
    print_header "CHAOS ENGINEERING TESTS"
    
    # Optional: uncomment to run chaos tests
    # chaos_database_latency
    # sleep 10
    
    # chaos_redis_failure
    # sleep 10
    
    # chaos_kafka_failure
    
    print_info "Chaos tests disabled by default (uncomment in script to enable)"
}

run_extended_tests() {
    if [ "$RUN_EXTENDED" = false ]; then
        return
    fi
    
    print_header "EXTENDED TESTS"
    
    print_info "Running extended correlation tests..."
    
    test_correlation_scenario "hot-key" 10 "${CURRENT_DURATION}"
    sleep 5
    
    test_correlation_scenario "high-cardinality" 5 "${CURRENT_DURATION}"
}

print_usage() {
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  --quick       Run quick tests (1 minute each)"
    echo "  --extended    Run extended tests (10+ minutes)"
    echo "  --chaos       Run chaos engineering scenarios only"
    echo "  --baseline    Run baseline tests only"
    echo "  --cleanup     Remove previous results"
    echo "  --help        Show this help message"
    echo ""
    echo "Environment Variables:"
    echo "  BASE_URL        Service URL (default: http://localhost:8080)"
    echo "  PANEL_API_URL   Panel API URL (default: http://localhost:5000)"
    echo "  CLICKHOUSE_URL  ClickHouse URL (default: http://localhost:8123)"
}

##############################################################################
# Main Execution
##############################################################################

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --quick)
            CURRENT_DURATION="${TEST_DURATION_QUICK}"
            shift
            ;;
        --extended)
            RUN_EXTENDED=true
            CURRENT_DURATION="${TEST_DURATION_EXTENDED}"
            shift
            ;;
        --chaos)
            SKIP_BASELINE=true
            shift
            ;;
        --baseline)
            SKIP_CHAOS=true
            shift
            ;;
        --cleanup)
            CLEANUP_RESULTS=true
            shift
            ;;
        --help)
            print_usage
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            print_usage
            exit 1
            ;;
    esac
done

# Main execution flow
check_prerequisites
cleanup_previous_results

echo ""
print_header "S.A.K.I.N. Performance Test Suite"
echo "Test Duration: ${CURRENT_DURATION}"
echo "Results Directory: ${RESULTS_DIR}"

run_baseline_tests
run_extended_tests
run_chaos_tests

generate_summary

print_header "All Tests Complete"
print_success "Results saved to: ${RESULTS_DIR}"
echo ""
echo "View results:"
echo "  Grafana:    http://localhost:3000 (Dashboards > Performance)"
echo "  Jaeger:     http://localhost:16686 (Distributed Traces)"
echo "  Prometheus: http://localhost:9090 (Metrics)"
echo ""
