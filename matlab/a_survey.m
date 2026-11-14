function a_survey(dataDir, outDir)
% NV09a: nhiet do/lux theo gio; tuong quan voi bat/tat quat, den. Ghi PNG + CSV vao outDir.
if ~exist(outDir, 'dir'), mkdir(outDir); end
S = load_csv(dataDir, 'sensor_reading'); C = load_csv(dataDir, 'device_command_log');
hr = hour(S.ts);
hours = (0:23)';
tempH = accumarray(hr + 1, S.temp_c, [24 1], @mean, NaN);
luxH  = accumarray(hr + 1, S.lux,    [24 1], @mean, NaN);
% trang thai quat/den tai moi mau cam bien (khong co lenh truoc do = tat)
res = table(hours, tempH, luxH, 'VariableNames', {'hour', 'mean_temp_c', 'mean_lux'});
rows = {};
for dev = ["fan" "light"]
    m = C.device == dev;
    on = state_at(C.ts(m), C.on(m), S.ts); on(isnan(on)) = 0;
    x = S.temp_c; if dev == "light", x = S.lux; end
    r = corrcoef(x, on); rows(end+1, :) = {char(dev), r(1,2), mean(on)}; %#ok<AGROW>
    S.(dev + "_on") = on;
end
corrT = cell2table(rows, 'VariableNames', {'device', 'corr_with_sensor', 'fraction_on'});
writetable(res, fullfile(outDir, 'a_hourly.csv')); writetable(corrT, fullfile(outDir, 'a_correlation.csv'));

f = figure('Visible', 'off', 'Position', [100 100 900 700]);
subplot(3,1,1); yyaxis left; plot(hours, tempH, '-o'); ylabel('Nhiet do (C)');
yyaxis right; plot(hours, luxH, '-s'); ylabel('Lux'); xlabel('Gio'); title('Trung binh theo gio');
subplot(3,1,2); edges = floor(min(S.temp_c)):1:ceil(max(S.temp_c));
b = discretize(S.temp_c, edges); ok = ~isnan(b);
p = accumarray(b(ok), S.fan_on(ok), [numel(edges)-1 1], @mean, NaN);
bar(edges(1:end-1) + .5, p); xlabel('Nhiet do (C)'); ylabel('Ty le quat bat'); title('Quat bat theo nhiet do');
subplot(3,1,3); edges = [0 5 10 20 30 50 100 200 500 1e5];
b = discretize(S.lux, edges); ok = ~isnan(b);
p = accumarray(b(ok), S.light_on(ok), [numel(edges)-1 1], @mean, NaN);
bar(p); set(gca, 'XTickLabel', arrayfun(@(e) sprintf('%g', e), edges(1:end-1), 'UniformOutput', false));
xlabel('Lux (can duoi cua nhom)'); ylabel('Ty le den bat'); title('Den bat theo lux');
saveas(f, fullfile(outDir, 'a_survey.png')); close(f);
disp(corrT);
end
