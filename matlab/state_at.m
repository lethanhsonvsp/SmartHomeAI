function s = state_at(tEvents, vEvents, tQuery)
% Gia tri cua chuoi su kien (buoc nhay) tai cac thoi diem tQuery; truoc su kien dau = NaN.
% vEvents: so (vd 0/1 hoac ma trang thai). tEvents phai tang dan.
[tEvents, i] = sort(tEvents); vEvents = vEvents(i);
s = interp1(datenum(tEvents), double(vEvents), datenum(tQuery), 'previous', NaN); %#ok<DATNM>
end
